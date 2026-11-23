using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class WarmingFixture
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public List<WarmingNotice> Notices { get; } = [];
        public List<MapPoint> Draws { get; } = [];
        public bool FailPresentation;
        public GlobalWarming Warming { get; }
        public WarmingFixture()
        {
            foreach (ref var tile in World.Map.Tiles) { tile = default; tile.Terrain = Terrain.Mountains; tile.Owner = null; }
            Warming = new(World, n => { Notices.Add(n); if (FailPresentation) throw new InvalidOperationException("Notice unavailable"); },
                p => { Draws.Add(p); if (FailPresentation) throw new InvalidOperationException("View unavailable"); });
        }
        public void Land(MapPoint p, Terrain terrain = Terrain.Grassland)
        { World.Map.TileAt(p).Terrain = terrain; }
    }
    private static void WarmingPressureArithmetic()
    {
        // Explicit W05 pressure vectors: AI tally truncation, human rounding and signed event count.
        foreach (var (l, a, humans, g, c, expected) in new[] {
            (7, 0, 2, 0, 8, 8), (8, 0, 3, 0, 6, 6), (-5, 0, 3, -2, 6, 6),
            (0, -3, 1, 0, 4, 4), (9, 5, 1, 0, 12, 12),
            (0, 0, 0, 0, 5, 4), (3, 0, 1, 0, 5, 6), (0, 0, 1, 0, 0, 0) })
        {
            var f = new WarmingFixture(); f.World.Globals.PollutedTiles = (short)l; f.World.Globals.WarmingPollution = (short)a;
            f.World.Globals.GlobalWarmings = (sbyte)g; f.World.Globals.WarmingCounter = (sbyte)c;
            f.World.Globals.HumanPlayers = default;
            for (int i = 0; i < humans; ++i) f.World.Globals.HumanPlayers = f.World.Globals.HumanPlayers.With(i);
            f.Warming.Update(); Equal((sbyte)expected, f.World.Globals.WarmingCounter);
            Equal((short)0, f.World.Globals.WarmingPollution); Equal((short)l, f.World.Globals.PollutedTiles);
            Equal((sbyte)g, f.World.Globals.GlobalWarmings);
        }
    }
    private static void WarmingSolarAdmission()
    {
        var f = new WarmingFixture(); f.World.Globals.PollutedTiles = 5; f.World.Globals.WarmingCounter = 8;
        f.World.Globals.CivsAlive = CivilizationSet.Only(1);
        foreach (var (slot, owner) in new[] { (0, 0), (1, 7), (2, 1) })
        { f.Data.AddCity(slot, owner, new(4 + slot * 2, 4)); BuildingQueries.SetBuilding(f.World, new((ushort)slot), BuildingIds.SolarPlant, true); }
        f.World.Entities.Cities.Storage[2].Token = default;
        f.Data.AddCity(3, 1, new(10, 4)); BuildingQueries.SetBuilding(f.World, new(3), BuildingIds.RecyclingCenter, true);
        f.Warming.Update(); Equal((sbyte)8, f.World.Globals.WarmingCounter); Equal(0, f.Notices.Count);
    }
    private static void WarmingWarningEquality()
    {
        var f = new WarmingFixture(); f.World.Globals.PollutedTiles = 8; f.World.Globals.GlobalWarmings = 1; f.World.Globals.WarmingCounter = 12;
        f.Warming.Update(); f.Warming.Update(); Equal(2, f.Notices.Count); Equal(WarmingNotice.FearWarming, f.Notices[0]);
        f.World.Multiplayer.Saved.AutoplayActive = true; f.Warming.Update(); Equal(2, f.Notices.Count);
        f.World.Multiplayer.Saved.AutoplayActive = false; f.World.Globals.PollutedTiles = 6; f.World.Globals.GlobalWarmings = 0;
        f.World.Globals.WarmingCounter = 11; f.Warming.Update(); Equal((sbyte)12, f.World.Globals.WarmingCounter); Equal(2, f.Notices.Count);
    }
    private static void WarmingEventSeverityAndCounters()
    {
        foreach (var (old, next, terrain) in new[] { (0, 1, Terrain.Plains), (-1, 0, Terrain.Grassland), (127, -128, Terrain.Swamp) })
        {
            var f = new WarmingFixture(); f.Land(new(8, 8)); f.World.Globals.PollutedTiles = 300;
            f.World.Globals.WarmingCounter = 16; f.World.Globals.GlobalWarmings = (sbyte)old; f.World.Globals.WarmingPollution = 3;
            f.Warming.Update(); Equal((sbyte)next, f.World.Globals.GlobalWarmings); Equal((sbyte)0, f.World.Globals.WarmingCounter);
            Equal((short)0, f.World.Globals.WarmingPollution); Equal((short)300, f.World.Globals.PollutedTiles);
            Equal(terrain, f.World.Map.TileAt(new(8, 8)).Terrain); Equal(WarmingNotice.GlobalWarming, f.Notices.Single());
        }
    }
    private static void WarmingPressureNarrowing()
    {
        var high = new WarmingFixture(); high.World.Globals.WarmingCounter = 127; high.World.Globals.PollutedTiles = 300;
        high.Warming.Update(); Equal((sbyte)0, high.World.Globals.WarmingCounter); Equal((sbyte)0, high.World.Globals.GlobalWarmings);
        var low = new WarmingFixture(); low.World.Globals.WarmingCounter = -128; low.World.Globals.PollutedTiles = -300;
        low.Warming.Update(); Equal((sbyte)0, low.World.Globals.WarmingCounter); Equal((sbyte)1, low.World.Globals.GlobalWarmings);
        Equal(WarmingNotice.GlobalWarming, low.Notices.Single());
    }
    private static void WarmingAutoplayAndObserverGates()
    {
        var f = new WarmingFixture(); var p = new MapPoint(8, 8); f.Land(p);
        f.World.Multiplayer.Saved.AutoplayActive = true; f.World.Scenario.Flags.NoPollution = true;
        f.World.Globals.WarmingCounter = 16; f.World.Globals.GlobalWarmings = 7; f.World.Globals.PollutedTiles = 40;
        f.World.Map.TileAt(p).Features = MapFeatures.Irrigation;
        f.World.Map.TileAt(p).RevealedTo = CivilizationSet.Only(1);
        f.World.Map.Remembered(1, p) = MapFeatures.Road;
        f.Warming.Update(); Equal(0, f.Notices.Count); Equal(0, f.Draws.Count);
        Equal(Terrain.Swamp, f.World.Map.TileAt(p).Terrain); Equal((sbyte)8, f.World.Globals.GlobalWarmings);
        Equal(MapFeatures.Road, f.World.Map.Remembered(1, p));
        f.Land(p); f.World.Multiplayer.LocalHuman = 0; f.World.Map.TileAt(p).RevealedTo = CivilizationSet.Only(0);
        f.Warming.Transform(7); Equal(0, f.Draws.Count); // Coastal memory/redraw excludes barbarian observer.
    }
    private static void WarmingCoastalRepeatedVisits()
    {
        foreach (bool repeatedSide in new[] { false, true })
        {
            var f = new WarmingFixture(); var p = new MapPoint(10, 10); f.Land(p);
            MapPoint[] offsets = repeatedSide ? [new(2, 0), new(1, 1), new(0, 2), new(-1, 1)]
                : [new(-2, 0), new(-1, -1), new(0, -2), new(1, -1)];
            foreach (var d in offsets) f.World.Map.TileAt(new(p.X + d.X, p.Y + d.Y)).Terrain = Terrain.Ocean;
            f.Warming.Transform(0); Equal(repeatedSide ? Terrain.Swamp : Terrain.Plains, f.World.Map.TileAt(p).Terrain);
        }
    }
    private static void WarmingCoastalMemoryAndFeatures()
    {
        var f = new WarmingFixture(); var p = new MapPoint(10, 10); f.Land(p, Terrain.Forest);
        ref var tile = ref f.World.Map.TileAt(p);
        tile.River = tile.ResourceSuppressed = true; tile.Region = 9; tile.Fertility = 7; tile.Claimant = 3;
        tile.Features = MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Railroad | MapFeatures.Pollution | MapFeatures.Unit | MapFeatures.CitySite;
        tile.RevealedTo = CivilizationSet.Only(1).With(2).With(4);
        f.World.Map.Remembered(1, p) = MapFeatures.Irrigation;
        f.World.Map.Remembered(2, p) = MapFeatures.Road;
        f.World.Map.Remembered(3, p) = MapFeatures.Mine;
        f.World.Map.Remembered(4, p) = MapFeatures.Mine;
        f.Warming.Transform(7);
        Equal(Terrain.Jungle, tile.Terrain); Equal((byte?)0, tile.Owner);
        Equal(MapFeatures.Road | MapFeatures.Railroad | MapFeatures.Pollution | MapFeatures.Unit | MapFeatures.CitySite, tile.Features);
        Equal(tile.Features, f.World.Map.Remembered(1, p)); Equal(tile.Features, f.World.Map.Remembered(4, p));
        Equal(MapFeatures.Road, f.World.Map.Remembered(2, p)); Equal(MapFeatures.Mine, f.World.Map.Remembered(3, p));
        Equal(p, f.Draws.Single()); Equal(true, tile.River); Equal(true, tile.ResourceSuppressed);
        Equal((byte)9, tile.Region); Equal((byte)7, tile.Fertility); Equal((byte)3, tile.Claimant);
        Equal((short)0, f.World.Globals.PollutedTiles);
    }
    private static void WarmingTerrainAndDryPattern()
    {
        for (int t = 0; t < 16; ++t)
        {
            var f = new WarmingFixture(); var p = new MapPoint(8, 8); f.Land(p, (Terrain)t);
            f.World.Map.TileAt(p).Owner = 3; f.Warming.Transform(7);
            Equal(t < 3 ? Terrain.Swamp : t == 3 ? Terrain.Jungle : (Terrain)t, f.World.Map.TileAt(p).Terrain);
            Equal((byte?)3, f.World.Map.TileAt(p).Owner);
        }
        foreach (int severity in new[] { -1, 0, 1, 2, 3, 4, 5, 6, 256 })
        {
            var f = new WarmingFixture(); MapPoint[] points = [new(8, 8), new(6, 8), new(4, 8), new(10, 8)];
            foreach (var p in points) { f.Land(p); f.World.Map.TileAt(p).Features = MapFeatures.Irrigation | MapFeatures.Mine; }
            f.Warming.Transform(severity);
            for (int i = 0; i < points.Length; ++i)
            { Equal(severity == 256 ? Terrain.Swamp : severity == i * 2 ? Terrain.Plains : Terrain.Grassland, f.World.Map.TileAt(points[i]).Terrain); }
            if (severity < 7) Equal(MapFeatures.Irrigation | MapFeatures.Mine, f.World.Map.TileAt(points[0]).Features);
        }
    }
    private static void WarmingDryKnowledge()
    {
        var f = new WarmingFixture(); var p = new MapPoint(8, 8); f.Land(p, Terrain.Desert);
        f.World.Map.TileAt(p).Features = MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Pollution;
        f.World.Map.TileAt(p).RevealedTo = CivilizationSet.Only(1); f.World.Map.Remembered(1, p) = MapFeatures.Road;
        f.Warming.Transform(0); Equal(p, f.Draws.Single()); Equal((byte?)0, f.World.Map.TileAt(p).Owner);
        Equal(Terrain.Desert, f.World.Map.TileAt(p).Terrain); Equal(MapFeatures.Road, f.World.Map.Remembered(1, p));
        Equal(MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Pollution, f.World.Map.TileAt(p).Features);
        f.Draws.Clear(); f.World.Map.TileAt(p).RevealedTo = default; f.Warming.Transform(0); Equal(0, f.Draws.Count);
    }
    private static void WarmingWrapAndBorders()
    {
        foreach (bool flat in new[] { false, true })
        {
            var f = new WarmingFixture(); f.World.Globals.Rules.FlatWorld = flat; f.World.Map.Allocate(new(4, 4));
            var p = new MapPoint(0, 0); f.Land(p);
            // Narrow-map aliases count independently; off-map northern visits do not count.
            f.Warming.Transform(0); Equal(flat ? Terrain.Plains : Terrain.Swamp, f.World.Map.TileAt(p).Terrain);
        }
    }
    private static void WarmingFailuresAndLocalAdmission()
    {
        var f = new WarmingFixture(); f.FailPresentation = true; f.Land(new(8, 8)); f.World.Map.TileAt(new(8, 8)).RevealedTo = CivilizationSet.Only(1);
        f.World.Globals.WarmingCounter = 16; f.World.Globals.PollutedTiles = 20; f.World.Globals.WarmingPollution = 2;
        Equal(2, f.Warming.Update().Count); Equal(Terrain.Plains, f.World.Map.TileAt(new(8, 8)).Terrain);
        Equal((short)0, f.World.Globals.WarmingPollution); Equal((sbyte)0, f.World.Globals.WarmingCounter);
        f.World.Globals.Multiplayer = GameMode.Tcp; f.World.Globals.WarmingCounter = 16;
        Throws<InvalidOperationException>(() => f.Warming.Update()); Equal((sbyte)16, f.World.Globals.WarmingCounter);
        Throws<InvalidOperationException>(() => f.Warming.Transform(7));
        var missing = new WarmingFixture(); missing.World.Map.Release(); missing.World.Globals.WarmingCounter = 16;
        missing.World.Globals.PollutedTiles = 20; missing.World.Globals.WarmingPollution = 2;
        Throws<InvalidOperationException>(() => missing.Warming.Update()); Equal((sbyte)17, missing.World.Globals.WarmingCounter);
        Equal((sbyte)1, missing.World.Globals.GlobalWarmings); Equal((short)2, missing.World.Globals.WarmingPollution);
    }
}
