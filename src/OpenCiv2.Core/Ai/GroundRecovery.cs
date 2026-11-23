using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public enum GroundRecoveryRoute { ScenarioGuard, CityGuard, TransportWork, PlanMove, Finish, Hold }
public readonly record struct GroundRecoveryResult(GroundRecoveryRoute Route, int DamageClass, bool PeacefulTerritory);

/// <summary>A04 §2 damage/recovery routing, after trespass and forced-city opportunity.
/// Uses real U01 stack queries and U04 GoHome. The parent owns the returned next phase.</summary>
public sealed class GroundRecovery(GameState world, RuleTables rules, StackOperations stacks, UnitRecovery recovery)
{
    public GroundRecoveryResult Evaluate(UnitTacticsContext context)
    {
        MapKnowledge.RequireLocal(world);
        var c = context;
        ref Unit Actor() => ref world.Entities.Units.At(c.Unit);
        UnitRule Rule() => rules.Units[Actor().Type.Value];
        int Count() => stacks.Count(c.Unit.Value);
        int Left() => UnitMovement.Left(world, rules, Actor());
        bool Adjacent() => SpatialControl.AdjacentNonalliedUnit(world, c.Position, c.Owner) is not null;
        bool air = c.Domain == RuleDomain.Air, land = c.Domain == RuleDomain.Land, sea = c.Domain == RuleDomain.Sea;
        int level = Actor().Damage == 0 ? 0 : 1;
        int hp = unchecked((sbyte)rules.Units[c.Type].HitPoints);
        if ((hp >> 2) < Actor().Damage)
        { level = 2; }
        if ((hp >> 1) < Actor().Damage)
        { level = 3; }
        bool peaceful = false;
        GroundRecoveryResult Route(GroundRecoveryRoute route) => new(route, level, peaceful);
        if (Rule().Capacity != 0 && Left() == 0)
        { c.NoMoves = true; return Route(GroundRecoveryRoute.TransportWork); }
        if (level != 0)
        {
            int claimant = world.Map.TileAt(c.Position).Claimant;
            if (claimant > 0)
            {
                var treaty = world.Civilizations[c.Owner].Treaties[claimant];
                peaceful = treaty.Peace && !treaty.Alliance
                    && world.Civilizations[claimant].Reputation - world.Civilizations[c.Owner].ReputationCredit[claimant] < 6;
            }
            if (c.NearestDistance == 0)
            { c.Hold = c.Guard = true; return Route(GroundRecoveryRoute.CityGuard); }
            if (c.NearestDistance == 1 && c.NearestCity == c.HomeCity)
            {
                UnitOrders.SetGoto(ref Actor(), AiTaskIds.Recover, world.Entities.Cities.At(c.HomeCity!.Value).Position);
                return Route(GroundRecoveryRoute.Finish);
            }
            if (c.NearestDistance == 1 && c.ForceAttack)
            { level = 0; }
        }
        if (level != 0 && Actor().MovesUsed == 0 && !peaceful && air && Rule().Range == 0 && !Adjacent()
            && c.HomeDistance >= 13 && Actor().Visibility == default
            && (level == 2 || Actor().Flags.Retreating || level == 3 && c.HomeDistance != 0 && c.HomeDistance >= 24 && c.NearestDistance >= 9))
        {
            Actor().Flags.Retreating = true;
            Actor().AiTask = AiTaskIds.Recover;
            return Route(GroundRecoveryRoute.Hold);
        }
        Actor().Flags.Retreating = false;
        if (level != 0 && Actor().MovesUsed == 0 && !peaceful
            && (land || sea && Count() < 2 && (level == 1 || (Actor().Damage >> 1) <= c.HomeDistance / 2))
            && (!sea || !Actor().Visibility.Intersects(world.Globals.HumanPlayers))
            && (level == 3 || unchecked((sbyte)Rule().Role) != c.Policy || sea)
            && (land ? !c.EnemyZone : !Adjacent()))
        {
            Actor().AiTask = AiTaskIds.Recover;
            return Route(GroundRecoveryRoute.Hold);
        }
        if (level == 2 && sea && (c.Role == (int)RuleRole.NavalSuperiority || c.Role == (int)RuleRole.SeaTransport && Count() < 2))
        {
            recovery.GoHome(c.Unit);
            if (Left() == 0)
            { return Route(GroundRecoveryRoute.Finish); }
            if (Actor().Orders == UnitOrder.GoTo)
            { c.HasOrders = true; return Route(GroundRecoveryRoute.PlanMove); }
        }
        if (level != 3)
        { return Route(GroundRecoveryRoute.ScenarioGuard); }
        Actor().AiTask = AiTaskIds.SevereRecovery;
        if (land && c.Role < 2 && (Actor().Flags.Garrison || unchecked((sbyte)rules.Terrain[(int)c.Terrain].DefenseFactor) > 2)
            && !peaceful && (c.Role == (int)RuleRole.Defend || UnitMovement.Allowance(world, rules, Actor()) <= rules.Cosmic.RoadMovementMultiplier
                || !c.EnemyZone || c.HomeDistance > 8))
        { return Route(GroundRecoveryRoute.Hold); }
        if (land && c.Fortress && !peaceful)
        { return Route(GroundRecoveryRoute.Hold); }
        if (c.Role != (int)RuleRole.SeaTransport || Count() < 2)
        {
            recovery.GoHome(c.Unit);
            if (Actor().Orders == UnitOrder.GoTo || Left() == 0)
            { return Route(GroundRecoveryRoute.Finish); }
        }
        return Route(GroundRecoveryRoute.ScenarioGuard);
    }
}
