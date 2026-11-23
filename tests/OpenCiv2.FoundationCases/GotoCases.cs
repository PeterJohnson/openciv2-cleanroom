using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U03 coarse snapping/waypoints and unit Goto; authored graph edges and game inputs.
    private sealed class GotoFixture
    {
        public RevelationFixture Data { get; } = new();
        public GenerationPathWorkspace State => Data.World.Paths;
        public CoarsePathfinding Coarse { get; }
        public UnitGoto Goto { get; }
        public GotoFixture(int budget = 1_000_000)
        {
            Data.World.Map.CoarseGrid(0).Fill(0); Data.World.Map.CoarseGrid(1).Fill(0);
            Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 1 };
            Data.Rules.Terrain[(int)Terrain.Ocean] = new() { MovementCost = 1 };
            Data.Rules.Units[20] = Data.Rules.Units[2];
            Data.Rules.Units[UnitTypeIds.Warriors] = Data.Rules.Units[0];
            Data.Rules.Units[UnitTypeIds.Caravel] = Data.Rules.Units[1];
            Coarse = new(Data.World, Data.Rules, Data.Stacks, budget);
            Goto = new(Data.World, Data.Rules, Data.Stacks, Coarse);
        }
        public void Edge(int x, int y, byte bits, int graph = 0) => Data.World.Map.SetCoarse(graph, new(x, y), bits);
        public void Actor(int type = 0)
        {
            Data.AddUnit(0, 1, type, new(10, 10));
            Data.Unit(0).Destination = new(16, 10); Data.Unit(0).Orders = UnitOrder.GoTo;
            Data.Unit(0).LastDirection = 255; State.Owner = 1;
        }
    }

    private static void CoarseSnapSelection()
    {
        var f = new GotoFixture(); f.Edge(2, 2, 4);
        Equal(true, f.Coarse.Snap(new(9, 9), false)); Equal(new MapPoint(2, 2), f.State.Goal);
        var distant = new GotoFixture(); distant.State.UniformCost = true;
        distant.Edge(3, 2, 4); // C east is not a D-offset candidate.
        Equal(false, distant.Coarse.Snap(new(9, 9), false)); Equal(new MapPoint(2, 2), distant.State.Goal);
        distant.Edge(4, 2, 64);
        Equal(true, distant.Coarse.Snap(new(9, 9), false)); Equal(new MapPoint(4, 2), distant.State.Goal);
        Equal(UnitTypeIds.Warriors, distant.State.SelectedUnitType);
        Throws<ArgumentOutOfRangeException>(() => distant.Coarse.Snap(new(-4, 9), false));
        var sea = new GotoFixture(); sea.State.UniformCost = true;
        foreach (ref var tile in sea.Data.World.Map.Tiles) tile.Terrain = Terrain.Ocean;
        sea.Edge(2, 2, 4, 1);
        Equal(true, sea.Coarse.Snap(new(10, 10), true)); Equal(new MapPoint(2, 2), sea.State.Goal);
        Equal(UnitTypeIds.Caravel, sea.State.SelectedUnitType);
    }

    private static void CoarseWaypointPaths()
    {
        var f = new GotoFixture(); f.Edge(2, 2, 4); f.Edge(3, 2, 68); f.Edge(4, 2, 64);
        Equal(true, f.Coarse.Waypoint(new(9, 9), new(17, 9)));
        Equal(new MapPoint(2, 2), f.State.CoarseStart); Equal(new MapPoint(13, 9), f.State.Goal);
        Equal((byte)3, f.Data.World.Map.Coarse(2, new(2, 2)));
        Equal((byte)2, f.Data.World.Map.Coarse(2, new(3, 2)));
        Equal((byte)1, f.Data.World.Map.Coarse(2, new(4, 2)));
        var same = new GotoFixture(); same.Edge(2, 2, 4);
        Equal(true, same.Coarse.Waypoint(new(9, 9), new(9, 9)));
        Equal(new MapPoint(9, 9), same.State.Goal); // Found does not imply an intermediate waypoint.
        var absent = new GotoFixture();
        Equal(false, absent.Coarse.Waypoint(new(9, 9), new(17, 9)));
        Equal(new MapPoint(17, 9), absent.State.Goal);
        absent.Edge(2, 2, 4);
        Equal(false, absent.Coarse.Waypoint(new(9, 9), new(29, 9)));
        Equal((byte)1, absent.Data.World.Map.Coarse(2, new(7, 2))); // Failed target snap still seeds BFS.
    }

    private static void CoarseWaypointTiesAndBudget()
    {
        var f = new GotoFixture(); f.Edge(2, 2, 10); f.Edge(4, 2, 160);
        f.Edge(3, 1, 40); f.Edge(3, 3, 130);
        Equal(true, f.Coarse.Waypoint(new(9, 9), new(17, 9)));
        Equal(new MapPoint(13, 5), f.State.Goal); // C[1] wins the exact distance tie with C[3].
        var bounded = new GotoFixture(1); bounded.Edge(2, 2, 4); bounded.Edge(3, 2, 68); bounded.Edge(4, 2, 64);
        Throws<InvalidOperationException>(() => bounded.Coarse.Waypoint(new(9, 9), new(17, 9)));
        Equal(new MapPoint(4, 2), bounded.State.Goal);
        Equal((byte)2, bounded.Data.World.Map.Coarse(2, new(3, 2)));
        Equal((byte)0, bounded.Data.World.Map.Coarse(2, new(2, 2)));
    }

    private static void GotoEarlyAndLocal()
    {
        var f = new GotoFixture(); f.Actor(); f.Data.Unit(0).MovesUsed = 2;
        foreach (var target in new[] { new MapPoint(-1, -1), new MapPoint(10, 10) })
        {
            f.Data.Unit(0).Destination = target; Equal(-1, f.Goto.Direction(new(0)));
            Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal((byte)2, f.Data.Unit(0).MovesUsed);
        }
        f.Data.Unit(0).Destination = new(12, 10); f.Data.World.Map.TileAt(new(12, 10)).Terrain = Terrain.Ocean;
        f.Data.AddUnit(1, 2, 0, new(12, 10));
        Equal(1, f.Goto.Direction(new(0))); // Adjacent selection does not admit movement.
        var local = new GotoFixture(); local.Actor(); Equal(1, local.Goto.Direction(new(0)));
        Equal(new MapPoint(16, 10), local.State.CachedGoal!.Value); Equal(37, local.State.ResultCost);
        Equal(new MapPoint(10, 10), local.Data.Unit(0).Position); Equal(UnitOrder.GoTo, local.Data.Unit(0).Orders);
        var seam = new GotoFixture(); seam.Actor(); seam.Data.World.Globals.Rules.FlatWorld = false;
        seam.Data.Stacks.AttachAtFront(0, new(0, 10), false); seam.Data.Unit(0).Destination = new(38, 10);
        Equal(5, seam.Goto.Direction(new(0)));
    }

    private static void GotoGreedyAndReversal()
    {
        foreach (int variant in new[] { 0, 1, 2 })
        {
            var f = new GotoFixture(); f.Actor(); f.State.RoadsOnly = true;
            f.Data.Unit(0).LastDirection = 5; f.Data.Unit(0).MovesUsed = 1;
            if (variant == 1) f.Data.World.Globals.HumanPlayers = default;
            if (variant == 2) f.Data.Unit(0).Flags.Automated = true;
            Equal(-1, f.Goto.Direction(new(0))); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
            Equal((byte)(variant == 0 ? 1 : 6), f.Data.Unit(0).MovesUsed); Equal(false, f.Data.Unit(0).Flags.Automated);
        }
        var road = new GotoFixture(); road.Actor(); road.State.RoadsOnly = true;
        Equal(1, road.Goto.Direction(new(0))); // Failed local/coarse searches reach actual Land fallback.
        var air = new GotoFixture(); air.Actor(20); air.Data.Unit(0).LastDirection = 5; air.Data.Unit(0).MovesUsed = 1;
        Equal(1, air.Goto.Direction(new(0))); Equal((byte)1, air.Data.Unit(0).MovesUsed);
        air.Data.World.Map.TileAt(new(12, 10)).Features |= (MapFeatures.CitySite | MapFeatures.Fortification);
        air.Data.World.Map.TileAt(new(12, 10)).Owner = 1;
        Equal(0, air.Goto.Direction(new(0))); // Equal diagonal scores keep the earlier direction.
    }

    private static void GotoNoCandidate()
    {
        foreach (bool automated in new[] { false, true })
        {
            var f = new GotoFixture(); f.Actor(20); f.Data.Unit(0).Flags.Automated = automated;
            foreach (var offset in MapNeighborhoods.Directions)
            {
                var p = new MapPoint(10 + offset.X, 10 + offset.Y);
                f.Data.World.Map.TileAt(p).Features |= (MapFeatures.CitySite | MapFeatures.Fortification);
                f.Data.World.Map.TileAt(p).Owner = 1;
            }
            Equal(-1, f.Goto.Direction(new(0))); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
            Equal((byte)(automated ? 6 : 0), f.Data.Unit(0).MovesUsed);
        }
    }

    private static void GotoCoarseRuleRetention()
    {
        var f = new GotoFixture(); f.Actor(1); f.Data.Unit(0).Destination = new(34, 10);
        foreach (var point in new[] { new MapPoint(9, 9), new MapPoint(10, 10), new MapPoint(34, 10) })
        { f.Data.World.Map.TileAt(point).Terrain = Terrain.Ocean; }
        // Customized fixed Caravel rules govern snapping and the second local search,
        // even though the actual actor remains a Sea unit and cannot cross this land.
        f.Data.Rules.Units[UnitTypeIds.Caravel] = f.Data.Rules.Units[0];
        for (int x = 2; x <= 8; ++x) f.Edge(x, 2, (byte)(x == 2 ? 4 : x == 8 ? 64 : 68), 1);
        Equal(0, f.Goto.Direction(new(0)));
        Equal(UnitTypeIds.Caravel, f.State.SelectedUnitType);
        Equal(new MapPoint(13, 9), f.State.Goal);
        Equal(new MapPoint(2, 2), f.State.CoarseStart);
        Equal(new MapPoint(10, 10), f.Data.Unit(0).Position);
        Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders);
    }
}
