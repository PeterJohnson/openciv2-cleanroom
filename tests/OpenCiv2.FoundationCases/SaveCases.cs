using System.Buffers.Binary;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Formats;
using OpenCiv2.IO;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // Authored F06 stream offsets for the cleanroom save codec fixtures.
    private const int MapOffset = 12 + 330 + 8 * 242 + 8 * 1428;
    private const int EntitiesOffset = MapOffset + 14 + 7 * 4 + 6 * 4 + 2 + 1024;
    private const int UsageOffset = EntitiesOffset + 2 * 32 + 88;
    private const int LeadersOffset = UsageOffset + 63 + 4;
    private const int HistoryOffset = LeadersOffset + 48;
    private const int LayoutOffset = HistoryOffset + 1200;
    private const int ViewsOffset = LayoutOffset + 106;
    private const int EliminationOffset = ViewsOffset + 80;
    private const int RequiredEnd = EliminationOffset + 338;
    private static void Put(byte[] bytes, int offset, params byte[] value) => value.CopyTo(bytes, offset);
    private static byte[] Pattern(int size) => Enumerable.Range(0, size).Select(i => unchecked((byte)i)).ToArray();
    private static void BytesEqual(IEnumerable<byte> expected, IEnumerable<byte> actual) => Equal(true, expected.SequenceEqual(actual));
    private static byte[] SaveVector()
    {
        var b = new byte[RequiredEnd];
        Put(b, 0, [.. "CIVILIZE\0"u8, 0x1a, 0x2c, 0]);
        Put(b, 12 + 0x10, 0xff, 0x7f, 0, 0x80); Put(b, 12 + 0x2e, 2, 0, 1, 0);
        Put(b, 12 + 330 + 2, (byte)'A', 0, 0xfe);
        Put(b, 12 + 330 + 8 * 242 + 2, 0, 0, 0, 0x80);
        Put(b, MapOffset, 4, 0, 2, 0, 4, 0, 0, 0, 24, 0, 1, 0, 1, 0);
        b[MapOffset + 14] = 0xa5; b[MapOffset + 14 + 7 * 4] = 0x8a;
        Put(b, EntitiesOffset, 2, 0, 1, 0); Put(b, EntitiesOffset + 26, 1, 0, 0, 0x80);
        b[EntitiesOffset + 32 + 8] = 0xee;
        Put(b, EntitiesOffset + 64 + 84, 0xff, 0xff, 0xff, 0xff);
        Put(b, UsageOffset, 13, 29, 1, 17, 31, 0); Put(b, UsageOffset + 63, 0xff, 0xff, 2, 0);
        b[LeadersOffset] = 0x80; b[HistoryOffset + 149 * 8 + 7] = 0xfe;
        Put(b, LayoutOffset + 2, 0xff, 0xff, 0xfe, 0xff, 0x7f, 0, 0x80, 0);
        Put(b, ViewsOffset + 8, 0xf9, 0xff); b[EliminationOffset + 0x32 + 11 * 24] = (byte)'Z';
        return b;
    }
    private static void Exact<T>(int size, ClassicFiles.Decoder<T> decode, Func<T, byte[]> encode)
    {
        byte[] bytes = Pattern(size); BytesEqual(bytes, encode(decode(bytes)));
        Throws<DecodeException>(() => decode(bytes.AsSpan(0, size - 1)));
        Throws<DecodeException>(() => decode([.. bytes, 0]));
    }
    private static void FixedRecordRoundtrips()
    {
        Exact(330, ClassicRecords.DecodeStoredGlobals, ClassicRecords.Encode);
        Exact(1428, ClassicRecords.DecodeStoredCivilization, ClassicRecords.Encode);
        Exact(242, ClassicRecords.DecodeStoredCivilizationNames, ClassicRecords.Encode);
        Exact(48, ClassicRecords.DecodeStoredLeader, ClassicRecords.Encode);
        Exact(106, ClassicRecords.DecodeSavedLayout, ClassicRecords.Encode);
        Exact(10, ClassicRecords.DecodeSavedMapView, ClassicRecords.Encode);
        Exact(100, ClassicRecords.DecodeScenarioParameters, ClassicRecords.Encode);
        Exact(338, ClassicRecords.DecodeEliminationHistory, ClassicRecords.Encode);
        Exact(1172, ClassicRecords.DecodeMultiplayerRecord, ClassicRecords.Encode);
        Exact(444, ClassicRecords.DecodeEventRecord, ClassicRecords.Encode);
        Exact(32, ClassicRecords.DecodeStoredUnit, ClassicRecords.Encode);
        Exact(88, ClassicRecords.DecodeStoredCity, ClassicRecords.Encode);
        Exact(14, ClassicRecords.DecodeMapHeader, ClassicRecords.Encode);
        Exact(16, ClassicRecords.DecodeRegion, ClassicRecords.Encode);
    }
    private static void FixedFieldOffsets()
    {
        byte[] unitBytes = [0x38, 0xff, 0x38, 0xff, 1, 0x80, 3, 7, 0xff, 0x81, 10, 0xff, 2, 0x80, 0xfe, 3,
            0xc8, 0xa5, 0xff, 7, 0, 0x80, 0xfe, 0xff, 0, 0, 1, 0, 0, 0x80, 0xef, 0xbe];
        var unit = ClassicRecords.DecodeStoredUnit(unitBytes);
        Equal((short)-200, unit.X); Equal((short)-200, unit.Y); Equal((ushort)0x8001, unit.Flags);
        Equal(new UnitType(3), unit.Type); Equal(new CivilizationSlot(7), unit.Owner); Equal((byte)255, unit.MovesUsed);
        Equal((byte)0x81, unit.Visibility); Equal((byte)10, unit.Damage); Equal((byte)0xfe, unit.AuxiliaryTimer);
        Equal(true, unit.Orders == UnitOrder.Sleeping); Equal((ushort)200, unit.Home!.Value.Value);
        Equal((short)2047, unit.DestinationX); Equal(short.MinValue, unit.DestinationY); Equal((short)-2, unit.Previous);
        Equal(new UnitToken(0x80000001), unit.Token); Equal((ushort)0xbeef, unit.RetainedWord);
        for (int home = 0; home < 256; ++home)
        {
            unitBytes[16] = (byte)home; var v = ClassicRecords.DecodeStoredUnit(unitBytes);
            Equal(true, home == 255 ? v.Home is null : v.Home!.Value.Value == home); BytesEqual(unitBytes, ClassicRecords.Encode(v));
        }
        Throws<ArgumentException>(() => ClassicRecords.Encode(unit with { Home = new CitySlot(255) }));
        var city = ClassicRecords.DecodeStoredCity(Pattern(88));
        Equal(0x07060504U, city.Flags); Equal((byte)20, city.RememberedSize[7]); Equal(0x19181716U, city.SpecialistKinds);
        Equal((byte)0x2f, city.Name[15]); Equal(0x33323130U, city.WorkedTiles); Equal((byte)0x38, city.Improvements[4]);
        Equal((short)0x4948, city.RoutePartners[2]); Equal(0x57565554U, city.Token.Value);
        var civ = ClassicRecords.DecodeStoredCivilization(Pattern(1428));
        Equal(0x05040302, civ.Gold); Equal((short)0x0706, civ.Tribe); Equal((byte)0x1a, civ.WondersByGroup[3]);
        Equal(0x23222120U, civ.Treaties[0]); Equal((byte)0x64, civ.Advances[12]); Equal((byte)0xd7, civ.AdvanceSource[99]);
        Equal((byte)0x15, civ.UnitsActive[61]); Equal((byte)0x53, civ.UnitsLost[61]); Equal((byte)0x91, civ.UnitsInProduction[61]);
        Equal((ushort)0x1312, civ.ContinentAttack[0]); Equal((short)-11310, civ.BestShieldSurplus); Equal((short)-7712, civ.GovernmentScore[6]);
        Equal((short)-3600, civ.LastContactTurn[7]); Equal((byte)0xf2, civ.LeaderPortrait); Equal((byte)0xfc, civ.ThroneRoom[9]);
        Equal((short)0x0302, civ.SpaceshipArrivalYear); Equal((sbyte)-109, civ.StandingGoals[15].Priority);
        var globals = ClassicRecords.DecodeStoredGlobals(Pattern(330));
        Equal(0x05040302U, globals.GameOptions); Equal((byte)0x99, globals.FirstDiscoverer[99]); Equal((byte)0xfd, globals.AdvanceKnownBy[99]);
        Equal((short)0x3736, globals.NextHistoryTurn); Equal((byte)0x49, globals.CivByRank[7]);
        var mp = ClassicRecords.DecodeMultiplayerRecord(Pattern(1172));
        Equal(0x33323130, mp.TurnTimeLimit); Equal((byte)0x33, mp.EncodedPasswords[255]); Equal((byte)0x63, mp.Emails[7][31]);
        Equal((byte)0x64, mp.ResumeCiv); Equal((short)0x6f6e, mp.DoubleMovement); Equal((byte)0x93, mp.Retained472[33]);
        var e = ClassicRecords.DecodeEventRecord(Pattern(444));
        Equal(0x0b0a0908U, e.UnitName); Equal(0x87868584U, e.Text[19]); Equal(-1145390664, e.MoveRectangle[7]);
        Equal(0x33323130, e.CreateLocations[19]); Equal((byte)0x73, e.Retained[39]); Equal(0x87868584U, e.WaveFile); Equal(-1145390664, e.GiveTechCiv);
    }
    private static void TileBitDomains()
    {
        byte[] bytes = [0xcb, 0x42, 0xe5, 0xff, 0x81, 0xef];
        for (int offset = 0; offset < 6; ++offset)
            for (int value = 0; value < 256; ++value)
            {
                var changed = bytes.ToArray(); changed[offset] = (byte)value;
                BytesEqual(changed, ClassicRecords.Encode(ClassicRecords.DecodeTile(changed)));
            }
        var tile = ClassicRecords.DecodeTile(bytes);
        Equal((byte)11, (byte)tile.Terrain); Equal(true, tile.Airbase); Equal((byte)7, tile.Claimant); Equal((byte)14, tile.Owner!.Value);
        Throws<ArgumentException>(() => ClassicRecords.Encode(tile with { Claimant = 8 }));
        Throws<ArgumentException>(() => ClassicRecords.Encode(tile with { Owner = 15 }));
        byte[] regionBytes = [0, 0x80, 0xff, 0x7f, 0xff, 0xff, 0xa5, 1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80, 0x5a];
        var region = ClassicRecords.DecodeRegion(regionBytes);
        Equal(short.MinValue, region.Land); Equal(short.MaxValue, region.Fertile); Equal((short)-1, region.Water);
        Equal(0x8040201008040201UL, region.NeighboringWater); BytesEqual([0xa5, 0x5a], region.Retained); BytesEqual(regionBytes, ClassicRecords.Encode(region));
    }
    private static void StoredStreamOrder()
    {
        byte[] bytes = [.. SaveVector(), (byte)'E', (byte)'V', (byte)'N']; bytes[9] = 0x55;
        var save = ClassicFiles.DecodeStoredSave(bytes);
        Equal(short.MaxValue, save.Globals.Turn); Equal(short.MinValue, save.Globals.Year); Equal(int.MinValue, save.Civilizations[0].Gold);
        Equal((byte)0xfe, save.Names[0].Leader[2]); Equal((byte)0xa5, save.Map.Remembered[0][0]); Equal(true, save.Map.Tiles[0].Ocean);
        Equal(2, save.Units.Count); Equal(0x80000001U, save.Units[0].Token.Value); Equal(0U, save.Units[1].Token.Value);
        Equal((byte)0xee, save.Units[1].MovesUsed); Equal(uint.MaxValue, save.Cities[0].Token.Value);
        Equal(new TribeUsage(13, 29, 1), save.Tribes[0]); Equal(new TribeUsage(17, 31, 0), save.Tribes[1]);
        Equal((short)-1, save.CursorX); Equal((short)2, save.CursorY); Equal((sbyte)-128, save.Leaders[0].Attack);
        Equal((byte)254, save.PowerHistory[149][7]); Equal((short)-2, save.Layout.Rectangles[0].Top); Equal((short)-7, save.Views[0].Zoom);
        Equal((byte)'Z', save.Eliminations.Names[11][0]); Equal(true, save.Events is null); BytesEqual("EVN"u8.ToArray(), save.Trailing);
        BytesEqual(bytes, ClassicFiles.EncodeStoredSave(save));
        var original = bytes.ToArray(); bytes[0] = 0; BytesEqual(original, ClassicFiles.EncodeStoredSave(save));
    }
    private static byte[] OptionalVector()
    {
        var bytes = SaveVector().ToList(); bytes[12 + 8] = 0x40; bytes[12 + 0x1a] = 1; bytes[12 + 0x23] = 0x85;
        bytes.InsertRange(HistoryOffset, new byte[96]); bytes[LeadersOffset + 48] = 17; bytes[LeadersOffset + 96] = 23;
        bytes.InsertRange(EliminationOffset + 96, new byte[100]); bytes[EliminationOffset + 96 + 2] = (byte)'S';
        bytes.AddRange(new byte[1172]); bytes[bytes.Count - 1172 + 0x46e] = 1;
        return bytes.ToArray();
    }
    private static void OptionalBlocks()
    {
        byte[] bytes = OptionalVector(); var save = ClassicFiles.DecodeStoredSave(bytes);
        Equal(3, save.Leaders.Count); Equal((sbyte)17, save.Leaders[1].Attack); Equal((sbyte)23, save.Leaders[2].Attack);
        Equal((byte)'S', save.Scenario!.Name[0]); Equal((short)1, save.Multiplayer!.DoubleMovement); BytesEqual(bytes, ClassicFiles.EncodeStoredSave(save));
        var noHumans = bytes.ToList(); noHumans[12 + 0x23] = 0; noHumans.RemoveRange(LeadersOffset, 144);
        Equal(0, ClassicFiles.DecodeStoredSave(noHumans.ToArray()).Leaders.Count);
        BytesEqual(noHumans, ClassicFiles.EncodeStoredSave(ClassicFiles.DecodeStoredSave(noHumans.ToArray())));
    }
    private static byte[] EventVector()
    {
        byte[] bytes = [.. SaveVector(), .. "EVNT"u8, 2, 0, 0, 0, .. new byte[888], 0, (byte)'A', 0, (byte)'B', 0, (byte)'C', 0, (byte)'W', 0, (byte)'D', 0, 0xfa, 0xfb];
        int first = RequiredEnd + 8;
        Put(bytes, first + 8, 1, 2, 3, 0x84); bytes[first + 0x38] = 2; bytes[first + 0x3c] = 3;
        bytes[first + 0x140] = 4; bytes[first + 0x184] = 5; bytes[first + 444 + 0x10] = 6;
        Put(bytes, first + 444 + 0x17c, 0, 0, 0, 0x80);
        return bytes;
    }
    private static void EventAdmission()
    {
        byte[] bytes = EventVector(); var save = ClassicFiles.DecodeStoredSave(bytes);
        Equal(2, save.Events!.Count); var e = save.Events[0]; Equal(0x84030201U, e.Record.UnitName);
        Equal(0, e.Strings[0]!.Count); Equal(true, e.Strings[1] is null); BytesEqual([(byte)'A'], e.Strings[4]!);
        BytesEqual([(byte)'C'], e.Strings[31]!); BytesEqual([(byte)'W'], e.Strings[34]!);
        Equal(int.MinValue, save.Events[1].Record.MoneyAmount); BytesEqual([0xfa, 0xfb], save.Trailing);
        BytesEqual(bytes, ClassicFiles.EncodeStoredSave(save));
        for (int end = RequiredEnd + 4; end < bytes.Length - 2; ++end)
            Throws<DecodeException>(() => ClassicFiles.DecodeStoredSave(bytes.AsSpan(0, end)));
        var bad = bytes.ToArray(); bad[RequiredEnd + 8 + 0x44] = 1; Throws<DecodeException>(() => ClassicFiles.DecodeStoredSave(bad));
        Put(bad, RequiredEnd + 4, 0xff, 0xff, 0xff, 0x7f); Throws<DecodeException>(() => ClassicFiles.DecodeStoredSave(bad));
        Put(bad, RequiredEnd + 4, 0xff, 0xff, 0xff, 0xff); Throws<DecodeException>(() => ClassicFiles.DecodeStoredSave(bad));
        byte[] boundary = [.. SaveVector(), .. "EVNT"u8, 1, 0, 0, 0, .. new byte[444], .. Enumerable.Repeat((byte)0xfe, 255), 0];
        boundary[RequiredEnd + 16] = 1; BytesEqual(boundary, ClassicFiles.EncodeStoredSave(ClassicFiles.DecodeStoredSave(boundary)));
        boundary[^1] = 0xfe; Throws<DecodeException>(() => ClassicFiles.DecodeStoredSave([.. boundary, 0]));
        byte[] empty = [.. SaveVector(), .. "EVNT"u8, 0, 0, 0, 0];
        Equal(0, ClassicFiles.DecodeStoredSave(empty).Events!.Count); BytesEqual(empty, ClassicFiles.EncodeStoredSave(ClassicFiles.DecodeStoredSave(empty)));
    }
    private static void RequiredBlockTruncation()
    {
        byte[] bytes = SaveVector();
        for (int end = 0; end < bytes.Length; ++end) Throws<DecodeException>(() => ClassicFiles.DecodeStoredSave(bytes.AsSpan(0, end)));
        foreach (byte version in new byte[] { 0x25, 0x29, 0x2a, 0x2b, 0x2d })
        {
            var bad = bytes.ToArray(); bad[10] = version; Throws<DecodeException>(() => ClassicFiles.DecodeStoredSave(bad));
        }
        foreach (int offset in new[] { 12 + 0x2e, 12 + 0x30 })
        {
            var bad = bytes.ToArray(); Put(bad, offset, 0xff, 0xff);
            try { ClassicFiles.DecodeStoredSave(bad); throw new InvalidOperationException("Negative scan bound accepted"); }
            catch (DecodeException error) { Equal(offset, error.Offset); }
        }
        var invalid = bytes.ToArray(); invalid[MapOffset] = 3; Throws<DecodeException>(() => ClassicFiles.DecodeStoredSave(invalid));
        invalid = bytes.ToArray(); Put(invalid, MapOffset + 4, 0xff, 0xff); Put(invalid, MapOffset + 10, 0, 0, 0, 0);
        Equal((short)-1, ClassicFiles.DecodeStoredSave(invalid).Map.Header.Area); BytesEqual(invalid, ClassicFiles.EncodeStoredSave(ClassicFiles.DecodeStoredSave(invalid)));
    }
    private static void ScanBoundsAndEncodingAdmission()
    {
        foreach (var (units, cities) in new[] { (0, 0), (2048, 256) })
        {
            var data = SaveVector().ToList(); data.RemoveRange(EntitiesOffset, UsageOffset - EntitiesOffset);
            data.InsertRange(EntitiesOffset, new byte[units * 32 + cities * 88]); var bytes = data.ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + 0x2e), (short)units);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + 0x30), (short)cities);
            var save = ClassicFiles.DecodeStoredSave(bytes); Equal(units, save.Units.Count); Equal(cities, save.Cities.Count);
            Equal(new TribeUsage(13, 29, 1), save.Tribes[0]); BytesEqual(bytes, ClassicFiles.EncodeStoredSave(save));
        }
        var original = ClassicFiles.DecodeStoredSave(SaveVector());
        StoredSave[] bad = [original with { Civilizations = new(original.Civilizations.Take(7)) },
            original with { Units = new([]) }, original with { Leaders = new([]) }, original with { Scenario = new() },
            original with { Multiplayer = new() }, original with { Header = new(0x1a, 0x28) },
            original with { Map = original.Map with { Remembered = new([]) } }, original with { Trailing = new("EVNT"u8.ToArray()) }];
        foreach (var save in bad) Throws<ArgumentException>(() => ClassicFiles.EncodeStoredSave(save));
        var strings = new OwnedList<byte>?[35];
        var record = new EventRecord { UnitName = 1 };
        Throws<ArgumentException>(() => ClassicFiles.EncodeStoredSave(original with { Events = new([new(record, new(strings))]) }));
        strings[0] = new([(byte)'A', 0, (byte)'B']);
        Throws<ArgumentException>(() => ClassicFiles.EncodeStoredSave(original with { Events = new([new(record, new(strings))]) }));
    }
    private static void LegacyLayouts()
    {
        foreach (byte version in new byte[] { 0x26, 0x27, 0x28 })
        {
            byte[] current = SaveVector(); var data = new List<byte>(current.AsSpan(0, 12).ToArray()); data[10] = version;
            void Part(int offset, int size) => data.AddRange(current.AsSpan(offset, size).ToArray());
            if (version < 0x28)
            {
                Part(12, 0x36); Part(12 + 0x36, 0x5d); Part(12 + 0x9a, 0x5d); Part(12 + 0xfe, 0x4c);
            }
            else Part(12, 330);
            Part(12 + 330, 8 * 242);
            for (int i = 0; i < 8; ++i)
            {
                int offset = 12 + 330 + 8 * 242 + i * 1428;
                if (version >= 0x28) Part(offset, 1428);
                else
                {
                    foreach (var (start, size) in new[] { (0, 0x58), (0x58, 0xc), (0x66, 0xe), (0x74, 0x5d), (0xd8, 0x36), (0x116, 0x36), (0x154, 0x36) }) Part(offset + start, size);
                    data.Add(0xa5); Part(offset + 0x192, 0x402);
                }
            }
            Part(MapOffset, EntitiesOffset - MapOffset); Part(EntitiesOffset, 26); Part(EntitiesOffset + 32, 26);
            Part(EntitiesOffset + 64, 84); Part(UsageOffset, RequiredEnd - UsageOffset);
            var save = ClassicFiles.DecodeStoredSave(data.ToArray());
            Equal((ushort)version, save.Header.Version); Equal(0U, save.Units[0].Token.Value); Equal(0U, save.Cities[0].Token.Value);
            Equal((byte)0xee, save.Units[1].MovesUsed); Equal(new TribeUsage(13, 29, 1), save.Tribes[0]);
            Equal(int.MinValue, save.Civilizations[0].Gold); Equal(0, save.Trailing.Count);
            if (version < 0x28) { Equal((byte)0, save.Globals.FirstDiscoverer[99]); Equal((byte)0, save.Civilizations[0].UnitsActive[61]); }
            Throws<ArgumentException>(() => ClassicFiles.EncodeStoredSave(save));
        }
    }
}
