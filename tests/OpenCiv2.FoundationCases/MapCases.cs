using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // W01 §§1–2,5–6,8–9: authored retained layers, feature gates and publication effects.
    private static void MapAllocationAndRelease()
    {
        var fallback = new TileCell { Value = new Tile { Owner = 3, Fertility = 7 } }; var map = new MapStorage(fallback) { PatternSeed = 24, StoredTopology = 1, RememberedBarbarian = [MapFeatures.CitySite | MapFeatures.Mine | MapFeatures.Railroad] };
        Throws<InvalidOperationException>(() => _ = map.Geometry); Throws<InvalidOperationException>(() => _ = map.Tiles.Length);
        map.Regions[5] = new(2, 3, 4, StateConversion.DecodeRegions(8)); map.Allocate(new(8, 4)); Equal(true, map.Allocated);
        for (int civ = 1; civ < 8; ++civ) Equal(new OwnedList<MapFeatures>(new MapFeatures[16]), new(map.RememberedLayer(civ).ToArray()));
        for (int grid = 0; grid < 4; ++grid) { Throws<InvalidOperationException>(() => map.Coarse(grid, new(0, 0))); Equal(true, map.CoarseGrid(grid).ToArray().All(c => c is null)); }
        foreach (var tile in map.Tiles) Equal(new Tile(), tile);
        Equal((byte)0, map.TileAt(new(0, 0)).Owner!.Value); map.ApplyClassic(new(ClassicTileField.Fertility, new(-1, 0), -1)); Equal((byte)15, map.TileAt(new(100, 50)).Fertility); Equal((byte)3, fallback.Value.Owner!.Value); Equal(true, map.Owner(new(-1, 0)) is null);
        map.TileAt(new(2, 1)).Region = 9; Equal((byte)9, map.TileAt(new(3, 1)).Region);
        map.Release(); Equal(false, map.Allocated); Equal(16, map.Geometry.Area); Equal((ushort)24, map.PatternSeed); Equal((ushort)1, map.StoredTopology); Equal(new RegionSummary(2, 3, 4, StateConversion.DecodeRegions(8)), map.Regions[5]);
        Equal(MapFeatures.CitySite | MapFeatures.Mine | MapFeatures.Railroad, map.Remembered(0, new(0, 0))); Equal((byte)15, map.TileAt(new(100, 50)).Fertility); Throws<InvalidOperationException>(() => map.TileAt(new(0, 0))); Throws<InvalidOperationException>(() => map.RememberedLayer(1));
        map.Release(); map.Allocate(new(4, 2)); Equal((byte)15, fallback.Value.Fertility); Equal((ushort)24, map.PatternSeed); Equal(4, map.Tiles.Length);
    }
    private static void MapReplacementAndSwap()
    {
        var firstFallback = new TileCell { Value = new Tile { Owner = 1 } }; var secondFallback = new TileCell { Value = new Tile { Owner = 7 } };
        var first = new MapStorage(firstFallback) { PatternSeed = 26, StoredTopology = 3, RememberedBarbarian = [MapFeatures.Irrigation] }; var second = new MapStorage(secondFallback);
        first.Allocate(new(8, 4)); first.TileAt(new(0, 0)).Region = 12; first.Remembered(2, new(0, 0)) = MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Road; first.SetCoarse(0, new(0, 0), 23);
        var oldTiles = first.Tiles; var oldMemory = first.RememberedLayer(2); var oldCoarse = first.CoarseGrid(0);
        first.Allocate(new(4, 2)); Equal((byte)12, oldTiles[0].Region); Equal(MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Road, oldMemory[0]); Equal((byte)23, oldCoarse[0]!.Value); Equal((byte)0, first.Tiles[0].Region); Equal(MapFeatures.None, first.Remembered(2, new(0, 0)));
        first.Tiles[0].Region = 31; first.Regions[1] = new(2, 3, 4, StateConversion.DecodeRegions(5)); second.SwapStorage(first);
        Equal(false, first.Allocated); Throws<InvalidOperationException>(() => _ = first.Geometry); Equal(true, second.Allocated); Equal(4, second.Geometry.Area); Equal((byte)31, second.Tiles[0].Region);
        Equal((ushort)26, second.PatternSeed); Equal((ushort)3, second.StoredTopology); Equal(MapFeatures.Irrigation, second.Remembered(0, new(0, 0))); Equal((short)2, second.Regions[1].Land); Equal((byte)7, second.TileAt(new(-1, 0)).Owner!.Value);
        first.Allocate(new(2, 2)); Equal((byte)1, first.TileAt(new(-1, 0)).Owner!.Value); second.SwapStorage(second); Equal((byte)31, second.Tiles[0].Region);
    }
    private static void MapDirectLayerIndices()
    {
        var map = new MapStorage(new()); map.Allocate(new(8, 8)); map.Remembered(1, new(6, 0)) = MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Irrigation | MapFeatures.Road | MapFeatures.Railroad;
        Equal(MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Irrigation | MapFeatures.Road | MapFeatures.Railroad, map.Remembered(1, new(-1, 1))); Equal(MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Irrigation | MapFeatures.Road | MapFeatures.Railroad, map.Remembered(1, new(7, 0)));
        Throws<ArgumentOutOfRangeException>(() => map.Remembered(1, new(-1, 0))); Throws<ArgumentOutOfRangeException>(() => map.Remembered(1, new(0, 8))); Throws<ArgumentOutOfRangeException>(() => map.Remembered(0, new(0, 0))); Throws<ArgumentOutOfRangeException>(() => map.RememberedLayer(8));
        map.SetCoarse(2, new(1, 0), 73); Equal((byte)73, map.Coarse(2, new(-1, 1))); Throws<InvalidOperationException>(() => map.Coarse(1, new(1, 0)));
        Throws<ArgumentOutOfRangeException>(() => map.SetCoarse(2, new(-1, 0), 9)); Throws<ArgumentOutOfRangeException>(() => map.SetCoarse(4, new(0, 0), 9));
        // Direct index arithmetic wraps before its backing check.
        map.Remembered(1, new(0, 0)) = MapFeatures.Unit | MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Railroad; Equal(MapFeatures.Unit | MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Railroad, map.Remembered(1, new(0, 1 << 30))); map.SetCoarse(0, new(0, 0), 62); Equal((byte)62, map.Coarse(0, new(0, int.MinValue)));
    }
    private static void MapRetainedSpatialReads()
    {
        var map = new MapStorage(new()); map.Allocate(new(8, 4)); ref var tile = ref map.TileAt(new(0, 0)); tile.Owner = 3; tile.Features = MapFeatures.CitySite | MapFeatures.Fortification;
        Equal((byte)3, map.AirbaseOwner(new(0, 0))!.Value); Equal(true, map.CityOwner(new(0, 0)) is null); Equal(true, map.UnitOwner(new(0, 0)) is null); Equal(true, map.CityOrUnitOwner(new(0, 0)) is null);
        map.ApplyClassic(new(ClassicTileField.Features, new(0, 0), 0x40, 0)); Equal((byte)3, map.CityOwner(new(0, 0))!.Value); Equal((byte)3, map.CityOrUnitOwner(new(0, 0))!.Value);
        foreach (byte fertility in new byte[] { 0, 1, 8, 9, 15 }) { tile.Fertility = fertility; Equal(fertility is >= 1 and <= 8 ? (byte)8 : fertility, map.ClaimantOrFertility(new(0, 0))); }
        tile.Claimant = 3; Equal((byte)3, map.ClaimantOrFertility(new(0, 0))); tile.Region = 7; Equal(true, map.LandRegion(new(0, 0)) is null); tile.Terrain = Terrain.Desert; Equal((byte)7, map.LandRegion(new(0, 0))!.Value);
        tile.RevealedTo = new CivilizationSet().With(0).With(3).With(7); foreach (var (civ, mask) in new (int, uint)[] { (-1, 1), (3, 8), (7, 128), (8, 0), (32, 1) }) Equal(mask, ClassicTileMutations.Revelation(map, new(0, 0), civ));
        MapPoint selected = new(7, 3); Equal(true, map.SelectTerrain(new(0, 0), true, true, ref selected)); Equal(new MapPoint(1, 1), selected);
        Equal(false, map.SelectTerrain(new(30, 4), false, false, ref selected)); Equal(new MapPoint(1, 1), selected); Equal(true, map.SelectTerrain(new(8, 0), false, false, ref selected)); Equal(new MapPoint(0, 0), selected);
    }
    private static void MapCoordinatePatterns()
    {
        var map = new MapStorage(new()); map.Allocate(new(8, 8)); ref var tile = ref map.TileAt(new(0, 0)); tile.Terrain = Terrain.Desert; tile.Owner = null; map.PatternSeed = 16;
        Equal((byte)2, map.SpecialResource(new(0, 0))); Equal(false, map.Hut(new(0, 0))); Equal((byte)0, map.SpecialResource(new(1, 0))); map.PatternSeed = 24; Equal(true, map.Hut(new(0, 0)));
        tile.ResourceSuppressed = true; tile.Features = MapFeatures.Unit; Equal(true, map.Hut(new(0, 0))); Equal((byte)0, map.SpecialResource(new(0, 0))); tile.Owner = 0; Equal(false, map.Hut(new(0, 0)));
        map.PatternSeed = 26; Equal((byte)1, map.SpecialResource(new(0, 2))); map.TileAt(new(0, 2)).Terrain = Terrain.Grassland; Equal((byte)0, map.SpecialResource(new(0, 2))); Equal((byte)0, map.SpecialResource(new(8, 0))); Equal(false, map.Hut(new(8, 0))); Equal((ushort)26, map.PatternSeed);
        Equal(true, Geometry.GrasslandShield(new(0, 0))); Equal(true, Geometry.GrasslandShield(new(2, 0))); Equal(false, Geometry.GrasslandShield(new(1, 1))); Equal(true, Geometry.GrasslandShield(new(int.MinValue, 0)));
    }
    private static void MapMutationFields()
    {
        var map = new MapStorage(new()); map.Allocate(new(4, 2)); ref var tile = ref map.TileAt(new(0, 0)); tile.Terrain = Terrain.Hills; tile.River = true; tile.Region = 7;
        map.Regions[7] = new(10, 0, 0, StateConversion.DecodeRegions(0)); map.Remembered(1, new(0, 0)) = MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Railroad | MapFeatures.Fortification; map.SetCoarse(0, new(0, 0), 9);
        map.ApplyClassic(new(ClassicTileField.Terrain, new(0, 0), 64)); Equal((byte)Terrain.Desert, (byte)tile.Terrain); Equal(true, tile.River); Equal(true, tile.ResourceSuppressed);
        map.ApplyClassic(new(ClassicTileField.Terrain, new(0, 0), 10)); Equal(true, tile.Ocean); Equal(true, tile.River); Equal(true, tile.ResourceSuppressed); Equal((byte)7, tile.Region); Equal((short)10, map.Regions[7].Land); Equal(MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Railroad | MapFeatures.Fortification, map.Remembered(1, new(0, 0))); Equal((byte)9, map.Coarse(0, new(0, 0)));
        map.ApplyClassic(new(ClassicTileField.Fertility, new(0, 0), -1)); map.ApplyClassic(new(ClassicTileField.Region, new(0, 0), -1)); map.ApplyClassic(new(ClassicTileField.Claimant, new(0, 0), -1));
        Equal((byte)15, tile.Fertility); Equal((byte)0, tile.Owner!.Value); Equal((byte)255, tile.Region); Equal((byte)7, tile.Claimant);
        map.ApplyClassic(new(ClassicTileField.Owner, new(0, 0), 8)); Equal((byte)8, map.Owner(new(0, 0))!.Value); map.ApplyClassic(new(ClassicTileField.Owner, new(0, 0), 9)); Equal(true, map.Owner(new(0, 0)) is null); map.ApplyClassic(new(ClassicTileField.Owner, new(0, 0), -1)); Equal(true, map.Owner(new(0, 0)) is null); Equal((byte)15, tile.Fertility);
        map.ApplyClassic(new(ClassicTileField.Revelation, new(0, 0), -1, -1)); map.ApplyClassic(new(ClassicTileField.Revelation, new(0, 0), 0x180, 0)); Equal(new CivilizationSet().With(0).With(1).With(2).With(3).With(4).With(5).With(6), tile.RevealedTo);
        map.ApplyClassic(new(ClassicTileField.Features, new(0, 0), 0x120)); Equal(MapFeatures.Railroad, tile.Features); var before = tile; Throws<ArgumentException>(() => map.ApplyClassic(new((ClassicTileField)7, new(0, 0), 0))); Equal(before, tile);
    }
    private static void MapSynchronization()
    {
        var fallback = new TileCell(); var map = new MapStorage(fallback); map.Allocate(new(4, 2)); var effects = new List<ClassicTileSyncEffect>();
        void Publish(ClassicTileSyncEffect effect) { Equal(MapFeatures.Unit, map.TileAt(effect.Mutation.Position).Features); effects.Add(effect); }
        var mutation = new ClassicTileMutation(ClassicTileField.Features, new(1, 0), 257, -2, true); map.ApplyClassic(mutation, new(3, true, true, false), Publish); Equal(new ClassicTileSyncEffect(TileSyncRoute.ClientBatch, mutation), effects.Single());
        map.ApplyClassic(mutation, new(3, true, true, true), Publish); Equal(1, effects.Count);
        foreach (var controls in new[] { new TileSyncControls(3, false, true, true), new TileSyncControls(3, true, false, true) }) { map.TileAt(new(0, 0)).Features = MapFeatures.None; map.ApplyClassic(mutation, controls, Publish); }
        Equal((int)TileSyncRoute.Broadcast, (int)effects[1].Route); Equal((int)TileSyncRoute.RequestHost, (int)effects[2].Route);
        foreach (var controls in new[] { new TileSyncControls(2, true, true, true), new TileSyncControls(3, false, true, false) }) { map.TileAt(new(0, 0)).Features = MapFeatures.None; map.ApplyClassic(mutation, controls, Publish); }
        Equal(3, effects.Count);
        map.ApplyClassic(new(ClassicTileField.Owner, new(-1, 0), 9, 1, true), new(3, false, false, true), effect => { Equal(true, fallback.Value.Owner is null); Equal(15, effect.Mutation.Operand); Equal(new MapPoint(-1, 0), effect.Mutation.Position); });
        var failure = new InvalidOperationException("authored publisher failure");
        try { map.ApplyClassic(new(ClassicTileField.Fertility, new(0, 0), -1, 1, true), new(3, true, false, true), effect => { Equal(-1, effect.Mutation.Operand); throw failure; }); throw new Exception("Expected publisher failure"); }
        catch (InvalidOperationException caught) { Equal(true, ReferenceEquals(failure, caught)); }
        Equal((byte)15, map.TileAt(new(0, 0)).Fertility); Throws<InvalidOperationException>(() => map.ApplyClassic(new(ClassicTileField.Region, new(0, 0), 9, 1, true), new(3, false, false, true))); Equal((byte)9, map.TileAt(new(0, 0)).Region);
        map.ApplyClassic(new(ClassicTileField.Region, new(0, 0), 10, 1, true), new(3, false, false, true), _ => map.Release()); Equal(false, map.Allocated);
    }
}
