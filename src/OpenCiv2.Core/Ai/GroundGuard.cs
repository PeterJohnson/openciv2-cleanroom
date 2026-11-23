using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public enum GroundGuardRoute { Specialized, PartisanFortress, CityGuard, TransportWork, PlanMove, Hold }

/// <summary>A04 §3 scenario/ferry gate, foreign-fortress routing and city guarding.
/// The caller runs actual specialized and partisan phases between these entry points.</summary>
public sealed class GroundGuard(GameState world, RuleTables rules, StackOperations stacks,
    CombatStrength strength, SharedGoals goals)
{
    private ref Unit Unit(UnitTacticsContext context) => ref world.Entities.Units.At(context.Unit);
    private UnitRule Rule(UnitTacticsContext context) => rules.Units[Unit(context).Type.Value];
    private static CitySlot Home(UnitTacticsContext context) => context.HomeCity
        ?? throw new InvalidOperationException("A04 guard city read requires the captured home city");
    private bool Adjacent(UnitTacticsContext c) => SpatialControl.AdjacentNonalliedUnit(world, c.Position, c.Owner, true) is not null;

    public GroundGuardRoute Scenario(UnitTacticsContext c)
    {
        MapKnowledge.RequireLocal(world);
        if (world.Globals.Status.Scenario && world.Scenario.Flags.HistoricalAi && c.Owner == 1
            && !world.Civilizations[1].Treaties[3].War && world.Globals.Turn <= 5 && Unit(c).Position.X <= 79)
        { return GroundGuardRoute.Hold; }
        if (c.Domain == RuleDomain.Land && c.HomeDistance == 0 && !Unit(c).Flags.Garrison
            && world.Entities.Cities.At(Home(c)).Flags.FerryPort && c.Policy != c.Role)
        { c.StayHome = true; return GroundGuardRoute.PartisanFortress; }
        return GroundGuardRoute.Specialized;
    }

    public GroundGuardRoute ForeignFortress(UnitTacticsContext c)
    {
        MapKnowledge.RequireLocal(world);
        if (!c.Fortress)
        { return GroundGuardRoute.CityGuard; }
        int claimant = world.Map.TileAt(c.Position).Claimant;
        if (claimant < 1 || claimant == c.Owner)
        { return GroundGuardRoute.CityGuard; }
        var treaty = world.Civilizations[c.Owner].Treaties[claimant];
        if (treaty.Peace && world.Civilizations[claimant].Reputation - world.Civilizations[c.Owner].ReputationCredit[claimant] < 7
            && (treaty.Alliance || c.HomeDistance > 2))
        { return GroundGuardRoute.CityGuard; }
        if (c.Role == (int)RuleRole.Defend)
        {
            if (stacks.CountRole(c.Unit.Value, (int)RuleRole.Defend, rules) > 1 && Unit(c).Orders == UnitOrder.GoTo)
            { return GroundGuardRoute.CityGuard; }
            Unit(c).AiTask = AiTaskIds.Fortify;
            c.Hold = true;
            return GroundGuardRoute.PlanMove;
        }
        if (c.Role == (int)RuleRole.Attack)
        {
            if (c.EnemyZone)
            { c.Hold = true; }
            if (stacks.Count(c.Unit.Value) == 1)
            {
                int obsolete = (int)Rule(c).ObsoleteBy;
                if (obsolete >= 0 && world.Globals.AdvanceKnownBy[obsolete] != default)
                { c.Hold = true; }
            }
            if (c.Hold)
            { return GroundGuardRoute.PlanMove; }
        }
        return GroundGuardRoute.CityGuard;
    }

    public GroundGuardRoute City(UnitTacticsContext c)
    {
        MapKnowledge.RequireLocal(world);
        if (c.HomeDistance == 0)
        { AssessCity(c); }
        if (!c.Guard)
        {
            if (c.StayHome)
            { c.Hold = true; }
            return c.Hold ? GroundGuardRoute.PlanMove : GroundGuardRoute.TransportWork;
        }
        int defense = unchecked((sbyte)Rule(c).Defense);
        if (BuildingQueries.HasBuilding(world, Home(c), BuildingIds.CityWalls))
        { defense *= 2; }
        else if (Unit(c).Flags.Garrison)
        { defense = 3 * defense / 2; }
        if (unchecked((sbyte)Rule(c).Attack) < defense)
        {
            Unit(c).AiTask = AiTaskIds.Fortify;
            Unit(c).Home = Home(c);
            c.StayHome = false;
            return GroundGuardRoute.Hold;
        }
        c.Hold = true;
        return GroundGuardRoute.PlanMove;
    }

    private void AssessCity(UnitTacticsContext c)
    {
        int defenders = stacks.CountRole(c.Unit.Value, (int)RuleRole.Defend, rules);
        int desired = 0;
        if (c.Role == (int)RuleRole.Defend && defenders > 1 || c.Role == (int)RuleRole.SeaTransport)
        {
            int divisor = Adjacent(c) ? 3 : 4;
            if (BuildingQueries.HasBuilding(world, Home(c), BuildingIds.Palace))
            { --divisor; }
            else if (c.Policy == 5)
            { ++divisor; }
            int size = world.Entities.Cities.At(Home(c)).Size;
            desired = size / divisor + 1;
            if (BuildingQueries.HasBuilding(world, Home(c), BuildingIds.Palace))
            {
                int floor = size > 3 ? 2 : 1;
                if (world.Civilizations[c.Owner].CityCount > 7)
                { floor = 3; }
                if (world.Civilizations[c.Owner].CityCount > 11)
                { floor = 4; }
                desired = Math.Max(floor, desired);
            }
            c.DesiredDefenders = desired;
        }
        if (c.Role != (int)RuleRole.Defend)
        {
            if (stacks.Count(c.Unit.Value) < 2)
            { goals.AddCurrent(c.Owner, c.Position, (int)RuleRole.Defend, 5); }
            c.Guard = !(c.SettlerType && !Adjacent(c));
            return;
        }
        if (defenders < 2 || strength.BestDefender(c.Unit, null) == c.Unit)
        { c.Guard = true; return; }
        if (defenders < desired)
        { goals.AddCurrent(c.Owner, c.Position, (int)RuleRole.Defend, 3); }
        if (defenders <= desired)
        { c.Guard = true; return; }
        var saved = Unit(c).Orders;
        Unit(c).Orders = UnitOrder.Fortified;
        if (strength.BestDefender(c.Unit, null) == c.Unit && stacks.CountReserveDefenders(c.Unit.Value, rules) <= desired)
        { Unit(c).Orders = saved; c.Guard = true; return; }
        Unit(c).Orders = saved;
        if ((world.Globals.Turn + c.Unit.Value) % 8 == 0 && saved is UnitOrder.Sleeping or UnitOrder.Fortifying or UnitOrder.Fortified)
        { Unit(c).Orders = UnitOrder.None; }
    }
}
