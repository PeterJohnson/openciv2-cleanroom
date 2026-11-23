using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public readonly record struct MovementOptions(bool Prompt, bool Execute);
/// <summary>U02 iteration captures, supplied after mission/combat/foreign-city entry handling.</summary>
public sealed record MovementStep(int Owner, RuleDomain Domain, MapPoint From, MapPoint To,
    Terrain DestinationTerrain, int Direction, UnitSlot? DestinationUnit = null, int? DestinationOwner = null, CitySlot? City = null);
public enum MovementChargeOutcome { Proceed, NoMovement, PartialFailure, Preview, Approach, Reversal, Crowding }
public sealed record MovementChargeResult(MovementChargeOutcome Outcome, int Cost)
{
    public bool Proceed => Outcome == MovementChargeOutcome.Proceed;
    public int ReturnValue => Outcome is MovementChargeOutcome.PartialFailure or MovementChargeOutcome.Preview ? 1 : 0;
}

/// <summary>U02 ordinary charge and ordered AI gates, using actual movement, geometry, control and
/// stack queries. A stop retains any charge/exhaustion; it does not perform dispatcher cleanup.</summary>
public sealed class MovementCharge(GameState world, RuleTables rules, StackOperations stacks, GameplayRandom random)
{
    public MovementChargeResult Apply(UnitSlot slot, MovementStep step, MovementOptions options, bool localHuman, string call)
    {
        MapKnowledge.RequireLocal(world);
        ref var unit = ref world.Entities.Units.At(slot);
        if (UnitMovement.Left(world, rules, unit) == 0)
        { return new(MovementChargeOutcome.NoMovement, 0); }
        int cost = UnitMovement.StepCost(world, rules, unit, step.Domain, step.From, step.To, step.DestinationTerrain);
        bool human = world.Globals.HumanPlayers.Contains(step.Owner);
        if (UnitMovement.Left(world, rules, unit) < cost && step.Domain == RuleDomain.Land && unit.MovesUsed != 0
            && (options.Prompt && (localHuman || world.Multiplayer.LocalHuman == step.Owner) || options.Execute && !human))
        {
            int roll = cost == 1 || unchecked(cost - 1) < 0 ? 0 : random.PrimaryModulo(new ChoiceKey("U02.partial-movement", call), cost);
            if (UnitMovement.Left(world, rules, unit) <= roll)
            {
                UnitMovement.Exhaust(world, rules, ref unit);
                return new(MovementChargeOutcome.PartialFailure, cost);
            }
        }
        if (!options.Execute)
        { return new(MovementChargeOutcome.Preview, cost); }
        int oldSpent = unit.MovesUsed;
        unit.MovesUsed = unchecked((byte)(oldSpent + cost));
        if (!human)
        {
            var rule = rules.Units[unit.Type.Value];
            bool diplomat = rule.Role == RuleRole.Diplomacy;
            if ((diplomat || rule.Role == RuleRole.Attack && 2 * unchecked((sbyte)rule.Defense) < unchecked((sbyte)rule.Attack))
                && unit.Orders == UnitOrder.GoTo && oldSpent != 0
                && UnitMovement.Left(world, rules, unit) <= (diplomat ? 0 : 1)
                && cost < UnitMovement.Allowance(world, rules, unit)
                && (world.Map.Geometry.StepDistance(step.To, unit.Destination, world.Globals.Rules.FlatWorld) == 1
                    || (SpatialControl.AdjacentNonalliedCivilization(world, step.To, step.Owner) is not null
                        || unit.Visibility.Intersects(world.Globals.HumanPlayers))
                    && SpatialControl.AdjacentNonalliedCivilization(world, step.From, step.Owner) is null))
            { return new(MovementChargeOutcome.Approach, cost); }
            if ((unchecked((sbyte)unit.LastDirection) ^ 4) == step.Direction && oldSpent != 0
                && rules.Units[unit.Type.Value].Domain != RuleDomain.Air && stacks.Count(slot.Value) < 3)
            {
                UnitMovement.Exhaust(world, rules, ref unit);
                return new(MovementChargeOutcome.Reversal, cost);
            }
            if (step.DestinationUnit is { } target && step.DestinationOwner == step.Owner && step.City is null
                && step.Domain == RuleDomain.Land && rules.Units[unit.Type.Value].Role != RuleRole.Diplomacy)
            {
                int left = UnitMovement.Left(world, rules, unit);
                if (stacks.Count(target.Value) >= (left == 0 ? 2 : 4))
                {
                    UnitMovement.Exhaust(world, rules, ref unit);
                    return new(MovementChargeOutcome.Crowding, cost);
                }
            }
        }
        return new(MovementChargeOutcome.Proceed, cost);
    }
}
