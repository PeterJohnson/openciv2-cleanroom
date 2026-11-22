using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Core;

/// <summary>U03 retained reverse-flood state for W02 connectivity and F05 accelerated roads.
/// Reuse within a generation or explicitly retained game; use a new workspace for a fresh game.
/// This is not the general unit-goto/occupancy pathfinder.</summary>
public sealed class GenerationPathWorkspace
{
    internal int[] Costs { get; } = new int[48 * 48];
    internal MapPoint[] Queue { get; } = new MapPoint[48 * 48];
    internal int Head, Tail;
    public MapPoint? CachedGoal { get; internal set; }
    public MapPoint Goal { get; internal set; }
    public int SelectedUnitType { get; internal set; }
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
        Goal = default;
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
        if (workspace.Owner >= 0)
        {
            throw new InvalidOperationException("Uniform connectivity requires an absent path owner");
        }
        var shape = map.Geometry;
        var goal = workspace.Goal;
        var unit = rules.Units[workspace.SelectedUnitType];
        bool naval = unit.Domain == RuleDomain.Sea;
        workspace.ResultCost = 0;
        var normalizedSource = Normalize(shape, source, goal, flat);
        if (workspace.CachedGoal != goal || Cost(workspace, normalizedSource, goal) == 0)
        {
            Flood(map, flat, rules, workspace, source, goal, naval, unit.Abilities.CoastBound, limit);
        }
        if (workspace.ResultCost >= limit)
        {
            return -1;
        }
        int sourceCost = Cost(workspace, normalizedSource, goal);
        int best = 19999, tie = 19999, direction = -1;
        for (int d = 0; d < 8; ++d)
        {
            var offset = MapNeighborhoods.Directions[d];
            var raw = new MapPoint(source.X + offset.X, source.Y + offset.Y);
            var neighbor = new MapPoint(shape.WrapX(raw.X, flat), raw.Y);
            if (!Inside(raw, goal) || !shape.InBounds(neighbor)
                || (map.TileAt(neighbor).Ocean != naval && neighbor != source && neighbor != goal))
            {
                continue;
            }
            int cost = Cost(workspace, Normalize(shape, raw, goal, flat), goal);
            if (cost == 0 || cost >= sourceCost)
            {
                continue;
            }
            // Uniform selection skips even the coast penalty; the reverse flood does not.
            int score = cost + 1;
            if (score <= best)
            {
                int weighted = shape.WeightedDistance(goal, raw, flat);
                if (score < best || weighted < tie)
                {
                    best = score;
                    tie = weighted;
                    direction = d;
                }
            }
        }
        if (direction < 0 && workspace.CachedGoal is { } cached)
        {
            workspace.Goal = cached;
        }

        return direction;
    }

    private static MapPoint Normalize(Geometry shape, MapPoint point, MapPoint center, bool flat)
    {
        if (flat)
        {
            return point;
        }
        int x = point.X - center.X;
        if (x < 0)
        {
            x += shape.Width;
        }
        if (x >= shape.Width / 2)
        {
            x -= shape.Width;
        }
        return new(x + center.X, point.Y);
    }

    private static bool Inside(MapPoint point, MapPoint goal) =>
        ((Math.Abs(point.X - goal.X) + Math.Abs(point.Y - goal.Y)) & ~1) < 48;

    private static int Index(MapPoint point, MapPoint center)
    {
        int dx = point.X - center.X, dy = point.Y - center.Y;
        int half = (dx + dy) >> 1;
        int column = 24 + half, row = 24 + dx - half;
        if (column is < 0 or >= 48 || row is < 0 or >= 48)
        {
            throw new ArgumentOutOfRangeException(nameof(point), "Outside the U03 local grid");
        }
        return row * 48 + column;
    }

    private static int Cost(GenerationPathWorkspace workspace, MapPoint point, MapPoint center) =>
        workspace.Costs[Index(point, center)];

    private static void Flood(
        MapStorage map,
        bool flat,
        RuleTables rules,
        GenerationPathWorkspace workspace,
        MapPoint source,
        MapPoint goal,
        bool naval,
        bool coastBound,
        int limit)
    {
        var shape = map.Geometry;
        bool startsOnLand = naval && !map.TileAt(source).Ocean;
        workspace.CachedGoal = goal;
        workspace.ResultCost = limit;
        Array.Clear(workspace.Costs);
        workspace.Head = 0;
        workspace.Tail = 1;
        workspace.Queue[0] = goal;
        workspace.Costs[Index(goal, goal)] = 1;
        for (int removed = 0; removed < workspace.Queue.Length && workspace.Head != workspace.Tail; ++removed)
        {
            var point = workspace.Queue[workspace.Head];
            workspace.Head = (workspace.Head + 1) % workspace.Queue.Length;
            int cost = Cost(workspace, point, goal);
            if (cost > workspace.ResultCost)
            {
                continue;
            }
            if (shape.WrapX(point.X, flat) == source.X && point.Y == source.Y)
            {
                workspace.ResultCost = cost;
                continue;
            }
            foreach (var offset in MapNeighborhoods.Directions)
            {
                var raw = new MapPoint(point.X + offset.X, point.Y + offset.Y);
                var neighbor = new MapPoint(shape.WrapX(raw.X, flat), raw.Y);
                if (!Inside(raw, goal) || !shape.InBounds(neighbor))
                {
                    continue;
                }
                var tile = map.TileAt(neighbor);
                if ((tile.Ocean != naval && neighbor != source && neighbor != goal)
                    || (startsOnLand && (tile.Region == 63 || map.Regions[tile.Region].Water == 1)))
                {
                    continue;
                }
                int candidate = cost + 1;
                if (coastBound && tile.ResourceSuppressed)
                {
                    candidate += 4 * rules.Cosmic.RoadMovementMultiplier;
                }
                int index = Index(raw, goal);
                if (workspace.Costs[index] == 0 || workspace.Costs[index] > candidate)
                {
                    workspace.Costs[index] = candidate;
                    workspace.Queue[workspace.Tail] = raw;
                    workspace.Tail = (workspace.Tail + 1) % workspace.Queue.Length;
                    if (workspace.Tail == workspace.Head)
                    {
                        return;
                    }
                }
            }
        }
    }
}
