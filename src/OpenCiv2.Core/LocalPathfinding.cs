using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>U03 local reverse flood and descending-direction selection. Shares the world's retained
/// goal-only cache with generation and roads; it is an estimate, not movement admission.</summary>
public static class LocalPathfinding
{
    public static int NextDirection(GameState world, RuleTables rules, StackOperations stacks,
        MapPoint source, int limit, int? incomingOwner = null) =>
        new Search(world.Map, world.Globals.Rules.FlatWorld, rules, world.Paths, world, stacks, false)
            .Run(source, limit, incomingOwner);

    public static int PathLength(GameState world, RuleTables rules, StackOperations stacks,
        MapPoint source, MapPoint goal, bool bySea, int limit, int? incomingOwner = null)
    {
        int distance = world.Map.Geometry.StepDistance(source, goal, world.Globals.Rules.FlatWorld);
        if (distance == 0)
        { return 0; }
        if (distance >= 23)
        { return -1; }
        var state = world.Paths;
        state.SelectedUnitType = bySea ? UnitTypeIds.Caravel : UnitTypeIds.Warriors;
        state.Goal = goal;
        int owner = state.Owner;
        state.Owner = -1;
        int direction = NextDirection(world, rules, stacks, source, limit, incomingOwner);
        int result = direction >= 0 ? state.ResultCost : direction;
        state.Owner = owner;
        return result;
    }

    internal static int UniformDirection(MapStorage map, bool flat, RuleTables rules,
        GenerationPathWorkspace state, MapPoint source, int limit)
    {
        if (state.Owner >= 0)
        { throw new InvalidOperationException("Uniform connectivity requires an absent path owner"); }
        return new Search(map, flat, rules, state, null, null, true).Run(source, limit, null);
    }

    private sealed class Search(MapStorage map, bool flat, RuleTables rules, GenerationPathWorkspace state,
        GameState? world, StackOperations? stacks, bool uniformOnly)
    {
        private MapPoint center;
        private bool naval, terrainCost, startsOnLand;
        private int? ownerScratch;
        private bool Uniform => uniformOnly || state.UniformCost;
        private UnitRule Rule => rules.Units[state.SelectedUnitType];
        private int Road => rules.Cosmic.RoadMovementMultiplier;
        private static int Abs(int value) => value > 0 ? value : unchecked(-value);
        private static bool Inside(MapPoint point, MapPoint goal) =>
            (unchecked(Abs(unchecked(point.X - goal.X)) + Abs(unchecked(point.Y - goal.Y))) & ~1) < 48;
        private MapPoint Normalize(MapPoint point)
        {
            if (flat)
            { return point; }
            int x = unchecked(point.X - center.X);
            if (x < 0)
            { x = unchecked(x + map.Geometry.Width); }
            if (x >= map.Geometry.Width / 2)
            { x = unchecked(x - map.Geometry.Width); }
            return new(unchecked(x + center.X), point.Y);
        }
        private int Index(MapPoint point)
        {
            int dx = unchecked(point.X - center.X), dy = unchecked(point.Y - center.Y);
            int half = unchecked(dx + dy) >> 1;
            int column = unchecked(24 + half), row = unchecked(24 + dx - half);
            if (column is < 0 or >= 48 || row is < 0 or >= 48)
            { throw new ArgumentOutOfRangeException(nameof(point), "Outside the U03 local grid"); }
            return row * 48 + column;
        }
        private int Cost(MapPoint point) => state.Costs[Index(point)];
        private MapPoint Wrapped(MapPoint point) => new(map.Geometry.WrapX(point.X, flat), point.Y);
        private static MapPoint Add(MapPoint point, MapPoint offset) => new(unchecked(point.X + offset.X), unchecked(point.Y + offset.Y));
        private bool Medium(MapPoint point, MapPoint source) => map.TileAt(point).Ocean == naval || point == source || point == state.CachedGoal;
        private (bool Road, bool Rail, bool River) Features(MapPoint point)
        {
            var tile = map.TileAt(point);
            bool city = map.CityOwner(point) is not null;
            return ((tile.Features & MapFeatures.Road) != 0 || city, (tile.Features & MapFeatures.Railroad) != 0 || city, tile.River);
        }
        private int? Step(MapPoint from, (bool Road, bool Rail, bool River) features, MapPoint to)
        {
            if (Uniform)
            { return 1; }
            var tile = map.TileAt(to);
            bool city = map.CityOwner(to) is not null;
            if (features.Rail && ((tile.Features & MapFeatures.Railroad) != 0 || city))
            { return 1; }
            if (Rule.Abilities.Alpine || features.Road && ((tile.Features & MapFeatures.Road) != 0 || city))
            { return 4; }
            if (state.RoadsOnly)
            { return null; }
            if (features.River && tile.River && map.Geometry.WrappedDx(from.X, to.X, flat) == 1 && Abs(unchecked(from.Y - to.Y)) == 1)
            { return 4; }
            return unchecked(terrainCost ? (sbyte)rules.Terrain[(int)tile.Terrain].MovementCost * Road * 4 : 4 * Road);
        }
        private int Coast(MapPoint point) => Rule.Abilities.CoastBound && map.TileAt(point).ResourceSuppressed ? unchecked(4 * Road) : 0;
        private int Penalties(MapPoint point, int stackFactor)
        {
            var currentWorld = world ?? throw new InvalidOperationException("Path owner penalties require a world");
            int scratch = ownerScratch ?? throw new InvalidOperationException("U03 nonuniform selection requires its incoming owner scratch when no occupancy query assigned it");
            int penalty = 0;
            var tile = map.TileAt(point);
            if (state.Owner == scratch && !currentWorld.Globals.HumanPlayers.Contains(scratch & 31)
                && (tile.Features & (MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Fortification)) == MapFeatures.Unit
                && SpatialControl.AdjacentNonalliedUnit(currentWorld, point, state.Owner) is not null)
            {
                var actualStacks = stacks ?? throw new InvalidOperationException("Path stack penalty requires stack operations");
                penalty = unchecked(stackFactor * actualStacks.CountRole(actualStacks.FirstAt(point), unchecked((sbyte)Rule.Role), rules));
            }
            if (TerritoryQueries.PeaceOwner(currentWorld, point, state.Owner) is not null && unchecked((sbyte)Rule.Role) < 5)
            { penalty = unchecked(penalty + 4 * Road); }
            return penalty;
        }

        public int Run(MapPoint source, int limit, int? incomingOwner)
        {
            center = state.Goal;
            state.GridCenter = center;
            naval = Rule.Domain == RuleDomain.Sea;
            terrainCost = Road < unchecked((sbyte)Rule.Movement);
            startsOnLand = naval && !map.TileAt(source).Ocean;
            ownerScratch = incomingOwner;
            state.ResultCost = 0;
            var normalizedSource = Normalize(source);
            if (state.CachedGoal != state.Goal || Cost(normalizedSource) == 0)
            { Flood(source, limit); }
            int direction = state.ResultCost < limit ? Select(source, normalizedSource) : -1;
            if (direction < 0 && state.CachedGoal is { } cached)
            { state.Goal = cached; }
            return direction;
        }
        private void Flood(MapPoint source, int limit)
        {
            state.CachedGoal = state.Goal;
            Array.Clear(state.Costs);
            state.Head = 0;
            state.Tail = 1;
            state.Queue[0] = state.Goal;
            state.Costs[Index(state.Goal)] = 1;
            state.ResultCost = limit;
            for (int removed = 0; removed < state.Queue.Length && state.Head != state.Tail; ++removed)
            {
                var point = state.Queue[state.Head];
                state.Head = (state.Head + 1) % state.Queue.Length;
                int cost = Cost(point);
                if (cost > state.ResultCost)
                { continue; }
                var from = Wrapped(point);
                if (from == source)
                { state.ResultCost = cost; continue; }
                var features = Features(from);
                foreach (var offset in MapNeighborhoods.Directions)
                {
                    var raw = Add(point, offset);
                    var neighbor = Wrapped(raw);
                    if (!Inside(raw, state.Goal) || !map.Geometry.InBounds(neighbor) || !Medium(neighbor, source))
                    { continue; }
                    var tile = map.TileAt(neighbor);
                    if (startsOnLand && (tile.Region == 63 || map.Regions[tile.Region].Water == 1))
                    { continue; }
                    int candidate = cost;
                    if (state.Owner >= 0)
                    {
                        ownerScratch = map.CityOrUnitOwner(neighbor) ?? -1;
                        if (ownerScratch >= 0 && ownerScratch != state.Owner)
                        { continue; }
                        candidate = unchecked(candidate + Penalties(neighbor, 16));
                    }
                    if (Step(from, features, neighbor) is not { } step)
                    { continue; }
                    candidate = unchecked(candidate + step + Coast(neighbor));
                    int index = Index(raw);
                    if (state.Costs[index] != 0 && state.Costs[index] <= candidate)
                    { continue; }
                    state.Costs[index] = candidate;
                    state.Queue[state.Tail] = raw;
                    state.Tail = (state.Tail + 1) % state.Queue.Length;
                    if (state.Tail == state.Head)
                    { return; }
                }
            }
        }
        private int Select(MapPoint source, MapPoint normalizedSource)
        {
            int best = 19999, tie = 19999, direction = -1;
            var features = Features(source);
            int sourceCost = Cost(normalizedSource);
            for (int d = 0; d < MapLimits.Directions; ++d)
            {
                var raw = Add(source, MapNeighborhoods.Directions[d]);
                var neighbor = Wrapped(raw);
                if (!Inside(raw, state.Goal) || !map.Geometry.InBounds(neighbor) || !Medium(neighbor, source))
                { continue; }
                int cost = Cost(Normalize(raw));
                if (cost != 0 && state.Owner >= 0)
                {
                    ownerScratch = map.CityOrUnitOwner(neighbor) ?? -1;
                    if (ownerScratch >= 0 && ownerScratch != state.Owner)
                    { continue; }
                }
                if (cost == 0 || cost >= sourceCost)
                { continue; }
                if (Step(source, features, neighbor) is not { } step)
                { continue; }
                if (!Uniform)
                { step = unchecked(step + Coast(neighbor) + Penalties(neighbor, 8)); }
                int score = unchecked(cost + step);
                if (score > best)
                { continue; }
                int distance = map.Geometry.WeightedDistance(state.Goal, raw, flat);
                if (score < best || distance < tie)
                { best = score; tie = distance; direction = d; }
            }
            return direction;
        }
    }
}
