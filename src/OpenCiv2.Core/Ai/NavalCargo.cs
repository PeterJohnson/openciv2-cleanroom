using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public readonly record struct NavalCargoResult(UnitRoleSet Roles, int Released, int ReservedDefenders);

/// <summary>A05 §2 complete-stack cargo scan. Released counts members whose orders were cleared,
/// not free holds; fortified land members away from home do not consume the guard quota.</summary>
public sealed class NavalCargo(GameState world, RuleTables rules, StackOperations stacks)
{
    public NavalCargoResult Scan(UnitTacticsContext context)
    {
        MapKnowledge.RequireLocal(world);
        UnitRoleSet roles = default;
        int released = 0, defenders = 0;
        int Quota() => context.DesiredDefenders ?? throw new InvalidOperationException("A05 cargo requires a reached desired-defender quota");
        for (int current = stacks.First(context.Unit.Value); current >= 0; current = stacks.Next(current))
        {
            ref var unit = ref world.Entities.Units.Storage[current];
            var rule = rules.Units[unit.Type.Value];
            if (rule.Domain != RuleDomain.Land)
            { continue; }
            int role = unchecked((sbyte)rule.Role);
            if (unit.Orders == UnitOrder.Fortified)
            {
                if (context.HomeDistance != 0)
                { continue; }
                ++defenders;
                if (defenders <= Quota() || context.Policy != 4 && released != 0)
                { continue; }
            }
            else if (role == (int)RuleRole.Defend && defenders < Quota())
            {
                ++defenders;
                unit.Orders = UnitOrder.Fortifying;
                continue;
            }
            unit.Orders = UnitOrder.None;
            roles = roles.With(role);
            ++released;
        }
        return new(roles, released, defenders);
    }
}
