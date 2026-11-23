using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A05 §2 complete sea prefix. Continue returns to A04's pillage/partisan/work phase;
/// ReturnOne bypasses common Finish after the real cargo/transport work.</summary>
public sealed class NavalSeaTactics(GameState world, NavalCargo cargo, NavalTransport transport, NavalGoals destinations, SharedGoals goals)
{
    public NavalChoice Choose(NavalTransportContext state, int damageClass, string call)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        MapKnowledge.RequireLocal(world);
        var c = state.Tactics;
        if (c.Domain != RuleDomain.Sea)
        { return new(NavalRoute.Continue); }
        state.ResetCargo(cargo.Scan(c));
        if (c.Role == (int)RuleRole.NavalSuperiority)
        {
            goals.ClearStandingNear(c.Owner, (int)RuleRole.NavalSuperiority, c.Position, 0);
            goals.ClearStandingNear(c.Owner, AiGoalRoles.AntiSubmarine, c.Position, 0);
        }
        if (c.Role == (int)RuleRole.SeaTransport)
        {
            var selected = transport.Choose(state, damageClass, call);
            if (selected.Route != NavalRoute.Continue)
            { return selected; }
        }
        if (c.NoMoves)
        { return new(NavalRoute.ReturnOne); }
        if (c.Role == (int)RuleRole.SeaTransport || !c.EnemyZone)
        { destinations.Choose(state, call); }
        return new(NavalRoute.Continue);
    }
}
