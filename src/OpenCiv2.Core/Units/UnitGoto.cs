using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U03 unit Goto direction with actual local/coarse searches and ordered greedy fallback.
/// U04 owns path-owner setup, movement execution and final arrival/order writes.</summary>
public sealed class UnitGoto(GameState world, RuleTables rules, StackOperations stacks, CoarsePathfinding coarse)
{
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    private static int Abs(int value) => value > 0 ? value : unchecked(-value);
    private static int Scaled(int a, int b)
    {
        int half = unchecked(a + b) >> 1;
        return unchecked(a > b ? 2 * a - (half - b + 1) : 2 * b - (half - a + 1));
    }
    public int Direction(UnitSlot slot)
    {
        MapKnowledge.RequireLocal(world);
        var shape = world.Map.Geometry;
        bool flat = world.Globals.Rules.FlatWorld;
        var target = Unit(slot).Destination;
        var source = Unit(slot).Position;
        var domain = Rule(slot).Domain;
        world.Paths.SelectedUnitType = Unit(slot).Type.Value;
        if (!shape.InBounds(target) || target == source)
        { return -1; }
        if (!shape.InBounds(source))
        { throw new InvalidOperationException("Goto requires an on-map actor"); }
        int owner = Unit(slot).Owner.Value;
        bool human = world.Globals.HumanPlayers.Contains(owner);
        int distance = shape.StepDistance(source, target, flat);
        if (distance < 2)
        {
            int dx = target.X - source.X;
            if (!flat)
            {
                if (target.X >= shape.Width - 2 && source.X < 2)
                { dx = -1; }
                else if (source.X >= shape.Width - 2 && target.X < 2)
                { dx = 1; }
            }
            return Geometry.DirectionOf(new(dx, target.Y - source.Y), MapNeighborhoods.Directions);
        }
        world.Paths.Goal = target;
        if (domain != RuleDomain.Air)
        {
            if (distance < 23)
            {
                int local = LocalPathfinding.NextDirection(world, rules, stacks, source, 999);
                if (local >= 0)
                { return local; }
            }
            if (coarse.Waypoint(source, target) && shape.StepDistance(source, world.Paths.Goal, flat) < 23)
            {
                // Snapping deliberately leaves its fixed Warriors/Caravel selector installed.
                int local = LocalPathfinding.NextDirection(world, rules, stacks, source, 998);
                if (local >= 0)
                { return local; }
            }
        }
        return Greedy(slot, source, target, domain, owner, human);
    }
    private int Greedy(UnitSlot slot, MapPoint source, MapPoint target, RuleDomain capturedDomain, int owner, bool human)
    {
        var shape = world.Map.Geometry;
        bool flat = world.Globals.Rules.FlatWorld;
        int dx = world.Paths.Goal.X - source.X, dy = world.Paths.Goal.Y - source.Y;
        if (!flat && Abs(dx) > shape.Width / 2)
        { dx = unchecked((shape.Width - Abs(dx)) * -Math.Sign(dx)); }
        if (dx == 0 && dy == 0)
        { Unit(slot).Orders = UnitOrder.None; UnitMovement.Exhaust(world, rules, ref Unit(slot)); return -1; }
        var from = world.Map.TileAt(source);
        bool road = from.Features.HasFlag(MapFeatures.Road), rail = from.Features.HasFlag(MapFeatures.Railroad), river = from.River;
        bool sourceZone = SpatialControl.InEnemyZone(world, source, owner) is not null;
        int distance = Scaled(Abs(dx), Abs(dy)), best = 9999, selected = 8;
        for (int d = 0; d < MapLimits.Directions; ++d)
        {
            var offset = MapNeighborhoods.Directions[d];
            var next = MapNeighborhoods.Offset(shape, source, offset, flat);
            int a = Abs(unchecked(dx - offset.X)), b = Abs(unchecked(dy - offset.Y)), g = Scaled(a, b);
            if (human && g > distance || !shape.InBounds(next))
            { continue; }
            var rule = Rule(slot);
            if (rule.Domain == RuleDomain.Air && world.Map.AirbaseOwner(next) is not null && next != world.Paths.Goal)
            { continue; }
            var tile = world.Map.TileAt(next);
            int? occupant = world.Map.UnitOwner(next);
            bool admitted = (occupant is null || occupant == owner)
                && (rule.Domain == RuleDomain.Air || tile.Ocean == (capturedDomain == RuleDomain.Sea)
                    && (!sourceZone || SpatialControl.InEnemyZone(world, next, owner) is null || rule.Abilities.IgnoreZonesOfControl));
            if (!admitted && !(next == target && world.Map.CityOwner(next) == owner))
            { continue; }
            int r = rules.Cosmic.RoadMovementMultiplier;
            int estimate = unchecked(4 * r);
            if (rule.Domain == RuleDomain.Land)
            {
                if (rail && tile.Features.HasFlag(MapFeatures.Railroad))
                { estimate = 1; }
                else if (road && tile.Features.HasFlag(MapFeatures.Road))
                { estimate = 4; }
                else if (river && tile.River && Abs(offset.X) == 1 && Abs(offset.Y) == 1)
                { estimate = 4; }
                else if (UnitMovement.Allowance(world, rules, Unit(slot)) > r)
                { estimate = unchecked((sbyte)rules.Terrain[(int)tile.Terrain].MovementCost * r * 4); }
            }
            int score = unchecked((g * 4 + estimate + a + b) * 4);
            if (g != 0 && occupant is not null)
            { score = unchecked(score + 1); }
            if (score < best)
            { best = score; selected = d; }
        }
        int last = unchecked((sbyte)Unit(slot).LastDirection);
        if (selected == 8 || last >= 0 && (last ^ 4) == selected && Rule(slot).Domain != RuleDomain.Air && Unit(slot).MovesUsed != 0)
        {
            Unit(slot).Orders = UnitOrder.None;
            if (!human)
            { UnitMovement.Exhaust(world, rules, ref Unit(slot)); }
            else if (Unit(slot).Flags.Automated)
            { Unit(slot).Flags.Automated = false; UnitMovement.Exhaust(world, rules, ref Unit(slot)); }
            return -1;
        }
        return selected;
    }
}
