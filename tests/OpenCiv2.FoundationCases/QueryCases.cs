using System.Text.Json;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Formats;
using OpenCiv2.Query;
using Access = OpenCiv2.Query.FileAccess;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static StoredSave QuerySample()
    {
        var s = ClassicFiles.DecodeStoredSave(SaveVector());
        return s with
        {
            Globals = s.Globals with { UnitEnd = 3, CityEnd = 2, ViewCiv = -1 },
            Units = new([new() { Token = new(0x80000001) }, new() { Owner = new(255), Damage = 255 }, new() { Token = new(uint.MaxValue) }]),
            Cities = new([new() { Token = new(17), Name = new([0xff, 0, 0xfe, .. new byte[13]]), Size = -128, FoodBox = -32768 }, new()]),
            Map = s.Map with { Regions = new(s.Map.Regions.Select((r, i) => i == 0 ? r with { NeighboringWater = ulong.MaxValue } : r)) }
        };
    }
    private static StoredSnapshot Inspect(StoredSave save) => StoredInspection.Inspect(ClassicFiles.EncodeStoredSave(save), FileKind.Save, Access.OfflineOmniscient);
    private static void OwnedRecordsAndDiffs()
    {
        var s = QuerySample(); byte[] bytes = ClassicFiles.EncodeStoredSave(s);
        var before = StoredInspection.Inspect(bytes, FileKind.Save, Access.OfflineOmniscient);
        Equal(true, before.Decoded); int end = 0;
        foreach (var record in before.Records) { Equal(end, record.Offset); end += record.Bytes.Count; }
        Equal(bytes.Length, end); BytesEqual(bytes, before.Records.SelectMany(r => r.Bytes));
        bytes[0] = 0; Equal((byte)'C', before.SourceBytes[0]);
        var unit = before.Records.Single(r => r.Key == new RecordKey(Section.Units, 0));
        Equal("2147483649", unit.Fields.Single(f => f.Name == "token").Text);
        Equal("255", before.Records.Single(r => r.Key == new RecordKey(Section.Units, 1)).Fields.Single(f => f.Name == "damage").Text);
        var changed = s with { Globals = s.Globals with { UnitEnd = 4 }, Units = new([s.Units[0] with { Home = new(0), Token = new(44) }, s.Units[1], s.Units[2], new() { Token = new(45) }]), Trailing = new([0xee]) };
        var after = Inspect(changed); var diff = StoredInspection.Diff(before, after);
        Equal(true, diff.FieldsCompared); Equal(true, diff.Different);
        Equal(false, diff.Fields.Any(f => f.Record.Section == Section.Cities));
        var home = diff.Fields.Single(f => f.Record == new RecordKey(Section.Units, 0) && f.Field == "home");
        Equal(true, home.Before is not null && home.Before.Value is null); Equal("0", home.After!.Text);
        Equal(true, diff.Fields.Where(f => f.Record == new RecordKey(Section.Units, 3)).All(f => f.Before is null));
        Equal(false, StoredInspection.Diff(before, before).Different);
        var unsupported = SaveVector(); unsupported[10] = 0x28;
        var old = StoredInspection.Inspect(unsupported, FileKind.Save, Access.OfflineOmniscient);
        Equal(true, old.Unsupported); Equal(false, StoredInspection.Diff(before, old).FieldsCompared);
        var shortInput = StoredInspection.Inspect(SaveVector().AsSpan(0, 12), FileKind.Save, Access.OfflineOmniscient);
        Equal(false, shortInput.Unsupported); Equal(false, shortInput.Decoded); Equal(12, shortInput.Diagnostics[0].Offset!.Value);
        foreach (byte[] vector in new[] { OptionalVector(), EventVector() })
        {
            var snapshot = StoredInspection.Inspect(vector, FileKind.Save, Access.OfflineOmniscient);
            Equal(true, snapshot.Decoded); BytesEqual(vector, snapshot.Records.SelectMany(r => r.Bytes));
        }
    }
    private static void SemanticDiagnostics()
    {
        var s = QuerySample();
        s = s with
        {
            Units = new([s.Units[0] with { Home = new(1), Next = 2, Previous = 2 }, s.Units[1], s.Units[2] with { Next = 0, Previous = 0, X = 2 }]),
            Cities = new([s.Cities[0] with { Name = new(Enumerable.Repeat((byte)'X', 16)), RouteCount = 4, RoutePartners = new([200, -1, -1]) }, s.Cities[1]]),
            Map = s.Map with { Header = s.Map.Header with { Area = 13 } }
        };
        var snapshot = Inspect(s); Equal(true, snapshot.Decoded); Equal(true, snapshot.HasErrors);
        foreach (string code in new[] { "map-size-mismatch", "unterminated-text", "route-count-range", "missing-city-reference", "stack-position-mismatch", "stack-cycle" })
            Equal(true, snapshot.Diagnostics.Any(d => d.Code == code));
        var city = snapshot.Records.Single(r => r.Key == new RecordKey(Section.Cities, 0));
        Equal(true, snapshot.Diagnostics.Any(d => d.Code == "unterminated-text" && d.Offset == city.Offset + 0x20));
    }
    private static void LosslessJsonAndRawMaps()
    {
        var snapshot = Inspect(QuerySample());
        using var document = JsonDocument.Parse(Output.SnapshotJson(snapshot));
        var root = document.RootElement;
        Equal("openciv2-inspection/1", root.GetProperty("schema").GetString()!);
        var values = root.GetProperty("records").EnumerateArray().SelectMany(r => r.GetProperty("fields").EnumerateArray()).ToArray();
        foreach (string value in new[] { "18446744073709551615", "-2147483648", "ff00fe00000000000000000000000000" })
            Equal(true, values.Any(f => f.GetProperty("value").ValueKind == JsonValueKind.String && f.GetProperty("value").GetString() == value));
        using var selection = JsonDocument.Parse(Output.SnapshotJson(snapshot, [Section.Cities]));
        Equal(true, selection.RootElement.GetProperty("records").EnumerateArray().All(r => r.GetProperty("section").GetString() == "cities"));
        Equal(true, selection.RootElement.GetProperty("coverage").GetProperty("omittedSections").EnumerateArray().Any(s => s.GetString() == "units"));
        var s = QuerySample();
        var raw = new RawMapFile(s.Map.Header, new(Enumerable.Range(0, 21).Select(i => (short)(i - 10))), new(Enumerable.Range(0, 21).Select(i => (short)(i * 3))), s.Map.Tiles, new([0xfe, 0x80]));
        byte[] bytes = ClassicFiles.EncodeRawMap(raw); Equal(raw, ClassicFiles.DecodeRawMap(bytes));
        var inspected = StoredInspection.Inspect(bytes, FileKind.RawMap, Access.OfflineOmniscient);
        Equal(true, inspected.Decoded); BytesEqual(bytes, inspected.Records.SelectMany(r => r.Bytes));
        Equal(14, inspected.Records.Single(r => r.Key == new RecordKey(Section.Starts, 0)).Offset);
        Equal(56, inspected.Records.Single(r => r.Key == new RecordKey(Section.Starts, 1)).Offset);
        Throws<ArgumentException>(() => StoredInspection.Inspect(bytes, FileKind.RawMap, (Access)1));
    }
    private static void GeometryContract()
    {
        var g = new Geometry(100, 80); Equal(254, g.TileIndex(new(8, 5))!.Value); Equal(254, g.TileIndex(new(9, 5))!.Value);
        Equal(false, g.Canonical(new(8, 5))); Equal(true, g.Canonical(new(9, 5))); Equal(true, g.TileIndex(new(100, 0)) is null);
        for (int i = 0; i < g.Area; ++i) { Equal(i, g.TileIndex(g.Coordinates(i))!.Value); Equal(true, g.Canonical(g.Coordinates(i))); }
        Throws<ArgumentOutOfRangeException>(() => g.Coordinates(g.Area));
        foreach (var (w, h) in new[] { (3, 4), (0, 4), (8, -1), (256, 256) }) Throws<ArgumentException>(() => new Geometry(w, h));
        Equal(32767, new Geometry(2, 32767).Area);
        foreach (var (input, output) in new[] { (-1, 99), (100, 0), (201, 101), (-201, -101) }) { Equal(output, g.WrapX(input, false)); Equal(input, g.WrapX(input, true)); }
        var coarse = new Geometry(10, 7); Equal(3, coarse.CoarseWidth); Equal(2, coarse.CoarseHeight); Equal(4, coarse.CoarseWrapX(7, false));
        Equal(2, g.StepDistance(new(98, 0), new(0, 2), false)); Equal(50, g.StepDistance(new(98, 0), new(0, 2), true));
        Equal(2, g.WeightedDistance(new(98, 0), new(0, 2), false)); Equal(5, Geometry.WeightedFromDeltas(new(6, 2)));
        Equal(-150, g.WrappedDx(0, 250, false)); Equal(-75, g.StepDistance(new(0, 0), new(250, 0), false)); Equal(112, g.WeightedDistance(new(0, 0), new(250, 0), false));
        Equal(int.MinValue, g.WrappedDx(int.MinValue, 0, false)); Equal(-1073741824, g.StepDistance(new(int.MinValue, 0), new(0, 0), true));
        Equal(0, g.StepDistance(new(int.MinValue, int.MinValue), new(0, 0), true)); Equal(-536870912, Geometry.WeightedFromDeltas(new(int.MinValue, 0)));
        Equal(-1073741825, Geometry.WeightedFromDeltas(new(int.MaxValue, int.MaxValue))); Equal(int.MinValue, g.WrappedDx(int.MaxValue, -1, true));
        MapPoint[] offsets = [new(1, 0), new(1, 0), new(9, 0), new(-1, 0)];
        Equal(new OwnedList<MapPoint>([new(1, 0), new(1, 0), new(3, 0)]), new Geometry(4, 4).Neighbors(new(0, 0), offsets, false));
        MapPoint[] directions = [new(1, -1), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1), new(0, -1), new(1, 0)];
        Equal((byte)0, Geometry.DirectionOf(new(20, -1), directions)); directions[7] = new(50, -2); Equal((byte)7, Geometry.DirectionOf(new(20, -1), directions));
        Equal((byte)8, Geometry.DirectionOf(new(0, 0), directions)); directions[6] = new(0, 0); Equal((byte)6, Geometry.DirectionOf(new(0, 0), directions));
        foreach (int x in new[] { 0, 4, 95 }) Equal(true, g.ContainsX(x, 95, 10, false));
        foreach (int x in new[] { 5, 94, -201 }) Equal(false, g.ContainsX(x, 95, 10, false));
        Equal(false, g.ContainsX(0, 95, 10, true)); Equal(false, g.ContainsX(95, 95, 0, false));
        Equal(true, g.ContainsRectangle(new(0, 9), new(95, 7), new(10, 3), false)); Equal(false, g.ContainsRectangle(new(0, 10), new(95, 7), new(10, 3), false));
    }
}
