using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>U03 coarse snapping and waypoint selection over the actual retained land/sea graphs.
/// The finite removal budget stops unsupported searches without returning an invented route.</summary>
public sealed class CoarsePathfinding(GameState world, RuleTables rules, StackOperations stacks, int removalBudget = 1_000_000)
{
    private MapStorage Map => world.Map;
    private Geometry Shape => Map.Geometry;
    private GenerationPathWorkspace State => world.Paths;
    private bool Flat => world.Globals.Rules.FlatWorld;
    private bool Inside(MapPoint cell) => cell.X >= 0 && cell.X < Shape.CoarseWidth && cell.Y >= 0 && cell.Y < Shape.CoarseHeight;
    private MapPoint Neighbor(MapPoint cell, MapPoint offset) => new(Shape.CoarseWrapX(cell.X + offset.X, Flat), cell.Y + offset.Y);
    private static MapPoint Base(MapPoint cell) => new(4 * cell.X + 1, 4 * cell.Y + 1);
    private bool Reachable(MapPoint cell, MapPoint tile, bool sea)
    {
        var representative = Base(cell);
        return Map.SelectTerrain(representative, sea, Flat, ref representative)
            && LocalPathfinding.PathLength(world, rules, stacks, representative, tile, sea, 18) >= 0;
    }
    public bool Snap(MapPoint tile, bool sea)
    {
        var containing = new MapPoint(tile.X >> 2, tile.Y >> 2);
        int graph = sea ? 1 : 0;
        // Reject unsupported coordinates before allowing a linear index to alias a different row.
        if (!Inside(containing))
        { throw new ArgumentOutOfRangeException(nameof(tile)); }
        if (Map.Coarse(graph, containing) != 0 && Reachable(containing, tile, sea))
        { State.Goal = containing; return true; }
        MapPoint chosen = containing;
        bool found = false;
        int best = 99;
        foreach (var offset in MapNeighborhoods.Directions)
        {
            var next = Neighbor(containing, offset);
            if (!Inside(next) || Map.Coarse(graph, next) == 0)
            { continue; }
            var basis = Base(next);
            if (!Shape.InBounds(basis))
            { continue; }
            int distance = Shape.WeightedDistance(tile, basis, Flat);
            if (distance < best && Reachable(next, tile, sea))
            { chosen = next; best = distance; found = true; }
        }
        State.Goal = chosen;
        return found;
    }
    public bool Waypoint(MapPoint source, MapPoint target)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(removalBudget);
        State.Goal = target;
        bool sea = rules.Units[State.SelectedUnitType].Domain == RuleDomain.Sea;
        int graph = sea ? 1 : 0;
        if (!Snap(source, sea))
        { State.Goal = target; return false; }
        var start = State.Goal;
        State.CoarseStart = start;
        Snap(target, sea); // Its fallback containing cell is still the BFS seed.
        var seed = State.Goal;
        Map.CoarseGrid(2).Fill(0);
        State.Head = 0;
        State.Tail = 1;
        State.Queue[0] = seed;
        Map.SetCoarse(2, seed, 1);
        bool found = false;
        int tieScore = 0, removed = 0;
        do
        {
            if (removed++ >= removalBudget)
            { throw new InvalidOperationException("U03 coarse search exceeded its finite removal budget"); }
            var cell = State.Queue[State.Head];
            State.Head = (State.Head + 1) & 255;
            if (cell == start)
            { found = true; break; }
            tieScore = Map.Coarse(2, cell);
            int connections = Map.Coarse(graph, cell);
            for (int d = 0; d < MapLimits.Directions; ++d)
            {
                if ((connections & (1 << d)) == 0)
                { continue; }
                var next = Neighbor(cell, MapNeighborhoods.Coarse[d]);
                if (!Inside(next) || Map.Coarse(2, next) != 0)
                { continue; }
                Map.SetCoarse(2, next, unchecked((byte)(tieScore + 1)));
                State.Queue[State.Tail] = next;
                State.Tail = (State.Tail + 1) & 255;
            }
        } while (State.Head != State.Tail);
        MapPoint? chosen = null;
        int best = 99;
        if (found)
        {
            int connections = Map.Coarse(graph, start);
            for (int d = 0; d < MapLimits.Directions; ++d)
            {
                if ((connections & (1 << d)) == 0)
                { continue; }
                var next = Neighbor(start, MapNeighborhoods.Coarse[d]);
                if (!Inside(next))
                { continue; }
                int distance = Map.Coarse(2, next);
                if (distance == 0 || distance > best)
                { continue; }
                int tie = Shape.WeightedDistance(target, Base(next), Flat);
                if (distance < best || tie < tieScore)
                { best = distance; chosen = next; tieScore = tie; }
            }
        }
        State.Goal = target;
        if (chosen is { } selected)
        {
            var waypoint = Base(selected);
            Map.SelectTerrain(waypoint, sea, Flat, ref waypoint);
            State.Goal = waypoint;
        }
        return found;
    }
}
