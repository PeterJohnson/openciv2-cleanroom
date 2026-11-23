using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public enum GroundOrderRoute { Continue, Finish, RetryTransport, Hold, PlanMove }

/// <summary>A04 retained Goto, DefenderFortify, direction application and common order finish.
/// These decide orders; U04 owns their eventual execution.</summary>
public sealed class GroundOrders(GameState world, RuleTables rules, StackOperations stacks, SharedGoals goals)
{
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    private bool Adjacent(UnitTacticsContext c) => SpatialControl.AdjacentNonalliedUnit(world, c.Position, c.Owner) is not null;

    public GroundOrderRoute RetainedGoto(UnitTacticsContext c)
    {
        MapKnowledge.RequireLocal(world);
        if (Unit(c.Unit).Orders != UnitOrder.GoTo || c.ForceAttack
            || c.Role == (int)RuleRole.NavalSuperiority && Adjacent(c))
        { return GroundOrderRoute.Continue; }
        int distance = world.Map.Geometry.StepDistance(c.Position, Unit(c.Unit).Destination, world.Globals.Rules.FlatWorld);
        if (distance > 1 && c.Role < 5 && Adjacent(c))
        { c.HasOrders = true; return GroundOrderRoute.Continue; }
        if (distance <= 1 && world.Map.CityOrUnitOwner(Unit(c.Unit).Destination) is { } other && other != c.Owner)
        {
            if (c.Role == (int)RuleRole.SeaTransport && stacks.Count(c.Unit.Value) >= 2 && !c.RetryTransport)
            {
                Unit(c.Unit).Orders = UnitOrder.None;
                c.RetryTransport = true;
                return GroundOrderRoute.RetryTransport;
            }
            var treaty = world.Civilizations[c.Owner].Treaties[other];
            if ((treaty.Ceasefire || treaty.Peace || treaty.Alliance)
                && !(c.Role == (int)RuleRole.Trade && world.Map.CityOwner(Unit(c.Unit).Destination) is not null))
            { Unit(c.Unit).Orders = UnitOrder.None; return GroundOrderRoute.Continue; }
        }
        return GroundOrderRoute.Finish;
    }

    public GroundOrderRoute DefenderFortify(UnitTacticsContext c)
    {
        MapKnowledge.RequireLocal(world);
        if (c.Role != (int)RuleRole.Defend || c.Terrain == Terrain.Ocean || c.HasOrders || c.ForceAttack)
        { return GroundOrderRoute.Continue; }
        bool alone = stacks.Count(c.Unit.Value) < 2 && c.HomeDistance < 4;
        bool danger = c.EnemyZone && stacks.CountRole(c.Unit.Value, (int)RuleRole.Defend, rules) == 1
            && (c.NearestCity is null || c.NearestCity == c.HomeCity || c.NearestDistance > 4 || DangerousCity());
        bool fort = c.FortressNeed != 0 || c.Fortress;
        bool lastFort = fort && stacks.CountRole(c.Unit.Value, (int)RuleRole.Defend, rules) == 1;
        bool holdFort = fort && (alone || danger || lastFort || Unit(c.Unit).Flags.Garrison);
        int attack = unchecked((sbyte)rules.Units[c.Type].Attack), defense = unchecked((sbyte)rules.Units[c.Type].Defense);
        if (attack >= defense)
        { alone = false; }
        if (attack > defense)
        { danger = holdFort = false; }
        int claimant = world.Map.TileAt(c.Position).Claimant;
        if (claimant > 0 && claimant != c.Owner && world.Civilizations[c.Owner].Treaties[claimant].Peace
            && world.Civilizations[claimant].Reputation - world.Civilizations[c.Owner].ReputationCredit[claimant] < 7)
        { alone = danger = lastFort = holdFort = false; }
        if (!(alone || danger || lastFort || holdFort))
        { return GroundOrderRoute.Continue; }
        if (lastFort || holdFort)
        { goals.ClearStandingNear(c.Owner, (int)RuleRole.Defend, c.Position, 1); }
        Unit(c.Unit).AiTask = AiTaskIds.Fortify;
        if (attack < defense)
        { return GroundOrderRoute.Hold; }
        c.Hold = true;
        return GroundOrderRoute.PlanMove;

        bool DangerousCity()
        {
            int other = world.Entities.Cities.At(c.NearestCity!.Value).Owner.Value;
            return world.Civilizations[c.Owner].Treaties[other].War
                || world.Civilizations[other].Reputation - world.Civilizations[c.Owner].ReputationCredit[other] > 6;
        }
    }

    public int Direction(UnitTacticsContext c, int direction)
    {
        MapKnowledge.RequireLocal(world);
        if (direction is < 0 or > 8)
        { throw new ArgumentOutOfRangeException(nameof(direction)); }
        if (direction == 8)
        { Unit(c.Unit).LastDirection = byte.MaxValue; Unit(c.Unit).Orders = UnitOrder.None; }
        else
        {
            c.Position = MapNeighborhoods.Offset(world.Map.Geometry, c.Position, MapNeighborhoods.Directions[direction], world.Globals.Rules.FlatWorld);
            if (world.Map.Geometry.InBounds(c.Position))
            {
                Unit(c.Unit).Orders = UnitOrder.AiGoTo;
                Unit(c.Unit).Destination = new(unchecked((short)c.Position.X), unchecked((short)c.Position.Y));
            }
            else
            { Unit(c.Unit).Orders = UnitOrder.None; UnitMovement.Exhaust(world, rules, ref Unit(c.Unit)); }
        }
        return Finish(c.Unit, c.Domain, c.StayHome);
    }

    public int Finish(UnitSlot slot, RuleDomain? capturedDomain, bool stayHome = false)
    {
        MapKnowledge.RequireLocal(world);
        if (Unit(slot).Orders is UnitOrder.None or UnitOrder.AiIdle or UnitOrder.Fortifying or UnitOrder.Fortified)
        {
            if (Unit(slot).Orders == UnitOrder.AiIdle)
            { Unit(slot).AiTask = AiTaskIds.Idle; }
            if (Rule(slot).Domain == RuleDomain.Air)
            {
                Unit(slot).Orders = UnitOrder.None;
                Unit(slot).MovesUsed = unchecked((byte)(Unit(slot).MovesUsed + rules.Cosmic.RoadMovementMultiplier));
                return 1;
            }
            UnitMovement.Exhaust(world, rules, ref Unit(slot));
            var domain = capturedDomain ?? throw new InvalidOperationException("A04 finish requires a produced captured domain on this order branch");
            if (domain == RuleDomain.Land && Rule(slot).Role != RuleRole.Settle)
            {
                Unit(slot).Orders = Unit(slot).Flags.Garrison ? UnitOrder.Fortified : UnitOrder.Fortifying;
                if (stayHome)
                { Unit(slot).Orders = UnitOrder.None; }
            }
            else
            { Unit(slot).Orders = UnitOrder.None; }
        }
        if (Unit(slot).Orders == UnitOrder.GoTo && Unit(slot).Destination == Unit(slot).Position)
        {
            Unit(slot).Flags.GoalReached = true;
            Unit(slot).Orders = UnitOrder.None;
            if (Unit(slot).MovesUsed != 0 && (Rule(slot).Role != RuleRole.NavalSuperiority
                || stacks.CountRole(slot.Value, (int)RuleRole.SeaTransport, rules) != 0))
            { UnitMovement.Exhaust(world, rules, ref Unit(slot)); }
        }
        return 0;
    }
}
