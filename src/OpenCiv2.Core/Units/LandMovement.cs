using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U02 local manual land steps. Combat, missions, huts and transport require their own callers.</summary>
public static class LandMovement
{
    public static int Left(GameState world, RuleTables rules, Unit unit) =>
        UnitMovement.Left(world, rules, unit);

    public static int Allowance(GameState world, RuleTables rules, Unit unit) =>
        UnitMovement.Allowance(world, rules, unit);

    /// <summary>Executes an ordinary land step and returns the committed outcome or a public denial code.</summary>
    public static (string Code, bool Changed) Step(
        GameState world, RuleTables rules, UnitSlot slot, int direction, GameplayRandom random, string call = "land-step")
    {
        ref var unit = ref world.Entities.Units.At(slot);
        var flags = unit.Flags;
        var rule = rules.Units[unit.Type.Value];
        if (direction is < 0 or >= MapLimits.Directions || rule.Domain != RuleDomain.Land
            || rule.Role is RuleRole.Diplomacy or RuleRole.Trade || flags.Aboard || flags.Automated
            || unit.Orders != UnitOrder.None || Left(world, rules, unit) == 0)
        {
            return ("step_not_available", false);
        }

        var map = world.Map;
        bool flat = world.Globals.Rules.FlatWorld;
        var from = unit.Position;
        var offset = MapNeighborhoods.Directions[direction];
        var to = MapNeighborhoods.Offset(map.Geometry, from, offset, flat);
        if (!map.Geometry.Canonical(to) || to == from || map.TileAt(from).Ocean)
        {
            return ("step_not_available", false);
        }

        int owner = unit.Owner.Value;
        var target = map.TileAt(to);
        if (!target.Unit && !rule.Abilities.IgnoreZonesOfControl && InZone(world, owner, from)
            && InZone(world, owner, to))
        {
            bool revealed = LocalVisibility.RevealAdjacentOccupants(world, owner, to);
            return ("zone_of_control", revealed);
        }

        if (target.Ocean || target.Airbase || (target.Unit || target.City) && target.Owner is byte occupant && occupant != owner
            || map.Hut(to))
        {
            return ("step_not_available", false);
        }

        var stacks = new StackOperations(world, new(100000, 64));
        int first = stacks.FirstAt(to);
        var step = new MovementStep(owner, rule.Domain, from, to, target.Terrain, direction,
            first < 0 ? null : new((ushort)first), map.CityOrUnitOwner(to), CityQueries.At(world, to));
        byte oldSpent = unit.MovesUsed;
        var charge = new MovementCharge(world, rules, stacks, random).Apply(slot, step, new(true, true),
            !world.Multiplayer.Saved.AutoplayActive && world.Multiplayer.LocalHuman == owner, call);
        if (!charge.Proceed)
        {
            return (charge.Outcome == MovementChargeOutcome.PartialFailure ? "movement_spent" : "step_not_available", unit.MovesUsed != oldSpent);
        }

        unit.Flags.Moved = true;
        unit.Flags.Garrison = false;
        unit.LastDirection = (byte)direction;
        unit.Visibility = default;
        stacks.Relocate(slot.Value, to, false);
        if (rule.Attack == 0)
        {
            stacks.MoveToBottom(slot.Value, false);
        }

        LocalVisibility.RevealAround(world, unit, rule);
        return ("moved", true);
    }

    /// <summary>U02 same-medium nonallied unit control. A city at the tested endpoint exempts it.</summary>
    private static bool InZone(GameState world, int owner, MapPoint origin) =>
        SpatialControl.InEnemyZone(world, origin, owner) is not null;
}
