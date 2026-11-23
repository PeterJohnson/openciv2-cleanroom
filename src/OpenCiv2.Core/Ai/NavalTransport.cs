using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A05 §3 SeaTransport controller, after the actual cargo scan. The A04 parent
/// handles returned direction/Finish choices and its subsequent NoMoves/direct-return gate.</summary>
public sealed class NavalTransport(GameState world, RuleTables rules, StackOperations stacks, NavalPorts ports, NavalUnload unload)
{
    public NavalChoice Choose(NavalTransportContext state, int damageClass, string call)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        MapKnowledge.RequireLocal(world);
        var c = state.Tactics;
        ref Unit Actor() => ref world.Entities.Units.At(c.Unit);
        if (c.NearestDistance == 0 && unchecked((sbyte)Actor().Counter) < 0)
        { Actor().Counter = (byte)(world.Globals.Turn & 7); }
        var settlers = default(UnitRoleSet).With((int)RuleRole.Settle);
        state.Room = state.Roles.Without((int)RuleRole.Trade) == settlers
            && state.Released < unchecked((sbyte)rules.Units[Actor().Type.Value].Capacity);
        state.OnlySettlersWithoutGoto = state.Roles == settlers && Actor().Orders != UnitOrder.GoTo;
        state.ExpandNeighbors = !c.NoMoves;
        if (!c.NoMoves && ports.Select(state, call) == NavalPortResult.Hold)
        { return new(NavalRoute.Direction, 8); }
        if (c.HomeDistance == 0)
        {
            for (int current = stacks.First(c.Unit.Value); current >= 0; current = stacks.Next(current))
            {
                if (rules.Units[world.Entities.Units.Storage[current].Type.Value].Role != RuleRole.Settle)
                { continue; }
                stacks.RelocateInPlace(current, true);
                break;
            }
        }
        if (state.ExpandNeighbors && Actor().AiTask == AiTaskIds.ReinforcePort && Actor().Orders == UnitOrder.GoTo
            && c.NearestDistance != 0 && CityQueries.At(world, Actor().Destination) is { } city
            && world.Entities.Cities.At(city).Flags.Coastal && CityQueries.TouchesBody(world, city, c.Region)
            && (world.Globals.Turn & 15) != 0)
        { state.ExpandNeighbors = false; }
        return state.ExpandNeighbors || c.RetryTransport || c.NoMoves ? unload.Choose(state, damageClass) : new(NavalRoute.Continue);
    }
}
