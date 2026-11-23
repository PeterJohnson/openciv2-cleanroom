using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Core;

/// <summary>U03 retained reverse-flood state for W02 connectivity and F05 accelerated roads.
/// Reuse within a generation or explicitly retained game; use a new workspace for a fresh game.
/// LocalPathfinding also consumes this workspace for ordinary unit/AI local searches.</summary>
public sealed class GenerationPathWorkspace
{
    internal int[] Costs { get; } = new int[48 * 48];
    internal MapPoint[] Queue { get; } = new MapPoint[48 * 48];
    internal int Head, Tail;
    public MapPoint? CachedGoal { get; internal set; }
    public MapPoint Goal { get; set; }
    public MapPoint GridCenter { get; internal set; }
    public int SelectedUnitType { get; set; }
    public int ResultCost { get; internal set; }
    public int Owner { get; set; } = -1;
    public bool UniformCost { get; set; }
    public bool RoadsOnly { get; set; }

    internal void Reset()
    {
        Array.Clear(Costs);
        Array.Clear(Queue);
        Head = Tail = 0;
        CachedGoal = null;
        Goal = GridCenter = default;
        SelectedUnitType = ResultCost = 0;
        Owner = -1;
        UniformCost = false;
        RoadsOnly = false;
    }
}

/// <summary>U03 local directions, bounded distance and coarse rebuilding in the uniform-cost, absent-owner domain.
/// Custom fixed-slot domains and coast penalties, FIFO limits and stale-goal cache reuse remain significant.</summary>
public static class GenerationConnectivity
{
    public static void Rebuild(MapStorage map, bool flat, RuleTables rules, GenerationPathWorkspace workspace)
    {
        workspace.UniformCost = true;
        for (int graph = 0; graph < 2; ++graph)
        {
            map.CoarseGrid(graph).Fill(0);
            var shape = map.Geometry;
            for (int cx = 0; cx < shape.CoarseWidth; ++cx)
            {
                for (int cy = 0; cy < shape.CoarseHeight; ++cy)
                {
                    var origin = new MapPoint(4 * cx + 1, 4 * cy + 1);
                    var source = origin;
                    if (!map.SelectTerrain(origin, graph == 1, flat, ref source))
                    {
                        continue;
                    }
                    for (int d = 0; d < 4; ++d)
                    {
                        var offset = MapNeighborhoods.Coarse[d];
                        var next = new MapPoint(shape.WrapX(origin.X + 4 * offset.X, flat), origin.Y + 4 * offset.Y);
                        var goal = next;
                        if (!shape.InBounds(next) || !map.SelectTerrain(next, graph == 1, flat, ref goal)
                            || map.TileAt(source).Region != map.TileAt(goal).Region)
                        {
                            continue;
                        }
                        int length = PathLength(map, flat, rules, workspace, source, goal, graph == 1, 5);
                        if (length is <= 0 or >= 5)
                        {
                            continue;
                        }
                        var cell = new MapPoint(cx, cy);
                        map.SetCoarse(graph, cell, (byte)(map.Coarse(graph, cell) | (1 << d)));
                        var other = new MapPoint(shape.CoarseWrapX(cx + offset.X, flat), cy + offset.Y);
                        if (other.X >= 0 && other.X < shape.CoarseWidth && other.Y >= 0 && other.Y < shape.CoarseHeight)
                        {
                            map.SetCoarse(graph, other, (byte)(map.Coarse(graph, other) | (1 << (d + 4))));
                        }
                    }
                }
            }
        }
        workspace.UniformCost = false;
    }

    /// <summary>U03 bounded query with uniform costs and no occupancy restrictions. A successful cache hit
    /// returns zero; a fresh result includes the goal's seed cost one. Incoming cache is not invalidated.</summary>
    public static int PathLength(
        MapStorage map,
        bool flat,
        RuleTables rules,
        GenerationPathWorkspace workspace,
        MapPoint source,
        MapPoint goal,
        bool bySea,
        int limit)
    {
        var shape = map.Geometry;
        int distance = shape.StepDistance(source, goal, flat);
        if (distance == 0)
        {
            return 0;
        }
        if (distance >= 23)
        {
            return -1;
        }
        workspace.SelectedUnitType = bySea ? UnitTypeIds.Caravel : UnitTypeIds.Warriors;
        workspace.Goal = goal;
        int owner = workspace.Owner;
        workspace.Owner = -1;
        int direction = UniformDirection(map, flat, rules, workspace, source, limit);
        int result = direction >= 0 ? workspace.ResultCost : direction;
        workspace.Owner = owner;
        return result;
    }

    /// <summary>U03 nextDirection for uniform cost and absent owner. Retains the goal-only cache, FIFO
    /// limits, coast penalties and directional ties. Callers own the mode/owner selectors; other path
    /// domains require the general movement pathfinder.</summary>
    public static int UniformDirection(
        MapStorage map, bool flat, RuleTables rules, GenerationPathWorkspace workspace, MapPoint source, int limit)
    {
        return LocalPathfinding.UniformDirection(map, flat, rules, workspace, source, limit);
    }
}
