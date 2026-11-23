using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class PathFixture
    {
        public RevelationFixture Data { get; } = new();
        public GenerationPathWorkspace State => Data.World.Paths;
        public MapPoint Source = new(10, 10), Middle = new(12, 10), Goal = new(14, 10);
        public PathFixture()
        {
            foreach (ref var tile in Data.World.Map.Tiles) tile.Terrain = Terrain.Ocean;
            foreach (var point in new[] { Source, Middle, Goal }) Data.World.Map.TileAt(point).Terrain = Terrain.Grassland;
            Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 1 };
            Data.Rules.Terrain[(int)Terrain.Ocean] = new() { MovementCost = 2 };
            State.Goal = Goal; State.Owner = 1; State.SelectedUnitType = 0;
        }
        public int Run(int limit = 200) => LocalPathfinding.NextDirection(Data.World, Data.Rules, Data.Stacks, Source, limit);
        public void Cost(MapPoint point, int cost)
        {
            // U03 logical grid coordinates, used only to supply a retained search input.
            int dx = point.X - Goal.X, dy = point.Y - Goal.Y;
            State.Costs[(24 + dx - ((dx + dy) >> 1)) * 48 + 24 + ((dx + dy) >> 1)] = cost;
            State.CachedGoal = Goal;
        }
    }

    private static void LocalPathBaseCosts()
    {
        foreach (var (variant, expected, direction) in new[] { (0, 25, 1), (1, 3, 1), (2, 9, 1), (3, 9, 1), (4, 200, -1), (5, 3, 1), (6, 61, 1) })
        {
            var f = new PathFixture();
            foreach (var point in new[] { f.Source, f.Middle, f.Goal })
            {
                if (variant == 1) f.Data.World.Map.TileAt(point).Features |= MapFeatures.Railroad;
                if (variant == 3) f.Data.World.Map.TileAt(point).Features |= MapFeatures.Road;
                if (variant == 5) { f.Data.World.Map.TileAt(point).Features |= MapFeatures.CitySite; f.Data.World.Map.TileAt(point).Owner = 1; }
            }
            if (variant == 2) f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Abilities = new() { Alpine = true } };
            if (variant == 4) f.State.RoadsOnly = true;
            if (variant == 6)
            {
                f.Data.World.Map.TileAt(f.Source).Terrain = Terrain.Hills; f.Data.World.Map.TileAt(f.Middle).Terrain = Terrain.Forest;
                f.Data.Rules.Terrain[(int)Terrain.Hills] = new() { MovementCost = 2 }; f.Data.Rules.Terrain[(int)Terrain.Forest] = new() { MovementCost = 3 };
            }
            Equal(direction, f.Run()); Equal(expected, f.State.ResultCost); Equal(f.Goal, f.State.GridCenter);
        }
        var river = new PathFixture(); river.Data.World.Map.TileAt(river.Middle).Terrain = Terrain.Ocean; river.Data.World.Map.TileAt(river.Goal).Terrain = Terrain.Ocean;
        river.Middle = new(11, 11); river.Goal = new(12, 12); river.State.Goal = river.Goal;
        foreach (var point in new[] { river.Source, river.Middle, river.Goal }) { river.Data.World.Map.TileAt(point).Terrain = Terrain.Grassland; river.Data.World.Map.TileAt(point).River = true; }
        Equal(2, river.Run()); Equal(9, river.State.ResultCost);
        var slow = new PathFixture(); slow.Data.Rules.Units[0] = slow.Data.Rules.Units[0] with { Movement = 3 };
        slow.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 9 }; Equal(1, slow.Run()); Equal(25, slow.State.ResultCost);
        var air = new PathFixture(); air.Data.Rules.Units[0] = air.Data.Rules.Units[0] with { Domain = RuleDomain.Air, Abilities = new() { Alpine = true } };
        Equal(1, air.Run()); Equal(9, air.State.ResultCost); // Local cost rules are not Land-only.
        var negative = new PathFixture(); negative.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 255 };
        // The three-tile corridor lowers source cost by 24 per cycle. Source is last removed
        // at removal 2302 (cycle 767), before the bounded flood stops at removal 2304.
        Equal(1, negative.Run()); Equal(1 - 24 * 767, negative.State.ResultCost);
    }

    private static void LocalPathOccupancyAndTreaties()
    {
        foreach (bool allied in new[] { false, true })
        {
            var f = new PathFixture(); f.Data.AddUnit(0, 2, 0, f.Middle); f.Data.World.Civilizations[1].Treaties[2].Alliance = allied;
            Equal(-1, f.Run()); Equal(200, f.State.ResultCost);
        }
        foreach (bool human in new[] { false, true })
            foreach (bool fortifiedTile in new[] { false, true })
            {
                var f = new PathFixture(); f.Data.World.Globals.HumanPlayers = human ? CivilizationSet.Only(1) : default;
                f.Data.AddUnit(0, 1, 0, f.Middle); f.Data.AddUnit(1, 1, 0, f.Middle); f.Data.AddUnit(2, 2, 0, new(12, 8));
                if (fortifiedTile) f.Data.World.Map.TileAt(f.Middle).Features |= MapFeatures.Fortification;
                Equal(1, f.Run()); Equal(human || fortifiedTile ? 25 : 57, f.State.ResultCost);
            }
        foreach (bool alliance in new[] { false, true })
            foreach (bool trade in new[] { false, true })
            {
                var f = new PathFixture(); f.Data.World.Map.TileAt(f.Middle).Claimant = 2;
                f.Data.World.Civilizations[1].Treaties[2].Peace = true; f.Data.World.Civilizations[1].Treaties[2].Alliance = alliance;
                if (trade) f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Trade };
                Equal(1, f.Run()); Equal(alliance || trade ? 25 : 37, f.State.ResultCost);
            }
        var uniform = new PathFixture(); uniform.State.UniformCost = uniform.State.RoadsOnly = true;
        uniform.Data.World.Globals.HumanPlayers = default;
        uniform.Data.AddUnit(0, 1, 0, uniform.Middle); uniform.Data.AddUnit(1, 1, 0, uniform.Middle); uniform.Data.AddUnit(2, 2, 0, new(12, 8));
        Equal(1, uniform.Run()); Equal(35, uniform.State.ResultCost); // Uniform flood still applies the stack penalty.
    }

    private static void LocalPathCacheAndLimits()
    {
        var f = new PathFixture(); Equal(-1, f.Run(25)); Equal(25, f.State.ResultCost);
        int head = f.State.Head, tail = f.State.Tail;
        f.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 7 };
        Equal(1, f.Run(1)); Equal(0, f.State.ResultCost); Equal(head, f.State.Head); Equal(tail, f.State.Tail);
        f.Data.AddUnit(0, 2, 0, f.Middle); Equal(-1, f.Run()); Equal(0, f.State.ResultCost); Equal(head, f.State.Head);
        f.State.Owner = 2; Equal(1, f.Run()); Equal(0, f.State.ResultCost);
        f.State.Goal = f.Middle; Equal(1, f.Run()); Equal(85, f.State.ResultCost); // A changed goal refloods with current terrain costs.
        var distance = new PathFixture(); distance.State.Owner = 7; distance.State.UniformCost = true;
        Equal(3, LocalPathfinding.PathLength(distance.Data.World, distance.Data.Rules, distance.Data.Stacks, distance.Source, distance.Goal, false, 20));
        Equal(7, distance.State.Owner); Equal(UnitTypeIds.Warriors, distance.State.SelectedUnitType);
        Equal(0, LocalPathfinding.PathLength(distance.Data.World, distance.Data.Rules, distance.Data.Stacks, distance.Source, distance.Goal, false, 20));
        distance.State.SelectedUnitType = 19;
        Equal(0, LocalPathfinding.PathLength(distance.Data.World, distance.Data.Rules, distance.Data.Stacks, distance.Source, distance.Source, false, 20));
        Equal(19, distance.State.SelectedUnitType); Equal(7, distance.State.Owner);
    }

    private static void LocalPathSelectionAndScratch()
    {
        var f = new PathFixture(); var diagonal = new MapPoint(11, 9); f.Data.World.Map.TileAt(diagonal).Terrain = Terrain.Grassland;
        f.Data.World.Globals.HumanPlayers = default; f.Data.AddUnit(0, 1, 0, f.Middle); f.Data.AddUnit(1, 1, 0, f.Middle); f.Data.AddUnit(2, 2, 0, new(12, 8));
        f.Cost(f.Source, 100); f.Cost(diagonal, 30); f.Cost(f.Middle, 10);
        Equal(1, f.Run()); // Selection adds 8 per matching role, not flood's 16.
        f.Cost(diagonal, 25); Equal(0, f.Run());
        f.State.UniformCost = true; Equal(1, f.Run());
        var tied = new PathFixture(); var north = new MapPoint(11, 9); var south = new MapPoint(11, 11);
        tied.Data.World.Map.TileAt(north).Terrain = tied.Data.World.Map.TileAt(south).Terrain = Terrain.Grassland;
        tied.Cost(tied.Source, 100); tied.Cost(north, 10); tied.Cost(south, 10); Equal(0, tied.Run());
        var unknown = new PathFixture(); unknown.State.Owner = -1;
        Equal(1, unknown.Run()); Equal(25, unknown.State.ResultCost);
        Equal(1, unknown.Run()); Equal(0, unknown.State.ResultCost);
        var threshold = new PathFixture(); threshold.Cost(threshold.Source, 30000); threshold.Cost(threshold.Middle, 20000);
        Equal(-1, threshold.Run()); // 19999 is a real selection threshold.
    }

    private static void LocalPathSeaAndSeam()
    {
        var f = new PathFixture(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Sea };
        foreach (ref var tile in f.Data.World.Map.Tiles) tile.Terrain = Terrain.Grassland;
        f.Data.World.Map.TileAt(f.Middle).Terrain = Terrain.Ocean; f.Data.World.Map.TileAt(f.Middle).Region = 63;
        Equal(-1, f.Run());
        f.Cost(f.Source, 100); f.Cost(f.Middle, 10); Equal(1, f.Run()); // Selection does not repeat starts-on-land's region gate.
        var coast = new PathFixture(); coast.State.UniformCost = true;
        coast.Data.Rules.Units[0] = coast.Data.Rules.Units[0] with { Abilities = new() { CoastBound = true } };
        coast.Data.World.Map.TileAt(coast.Middle).ResourceSuppressed = true;
        Equal(1, coast.Run()); Equal(15, coast.State.ResultCost);
        var seam = new PathFixture(); seam.Data.World.Globals.Rules.FlatWorld = false;
        seam.Data.World.Map.Allocate(new(80, 24));
        foreach (ref var tile in seam.Data.World.Map.Tiles) tile.Terrain = Terrain.Ocean;
        seam.Source = new(0, 10); seam.Goal = new(78, 10); seam.State.Goal = seam.Goal;
        seam.Data.World.Map.TileAt(seam.Source).Terrain = seam.Data.World.Map.TileAt(seam.Goal).Terrain = Terrain.Grassland;
        Equal(-1, seam.Run()); Equal(13, seam.State.ResultCost); // Raw neighbor window precedes seam normalization.
        var fresh = new PathFixture(); fresh.State.CachedGoal = fresh.Goal; fresh.Source = new(1000, 1000);
        Throws<ArgumentOutOfRangeException>(() => fresh.Run()); Equal(fresh.Goal, fresh.State.GridCenter);
    }
}
