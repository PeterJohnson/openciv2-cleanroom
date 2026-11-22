using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U04 eligibility and next-unit search. Activation, order dispatch and revelation remain callers.</summary>
public static class UnitActivation
{
    /// <summary>U04 selectable predicate: bounded coordinates, current owner, unslept/unfortified,
    /// remaining movement and no spy mission. Waiting and automated orders are not direct exclusions.</summary>
    public static bool Selectable(GameState world, RuleTables rules, int slot)
    {
        var pool = world.Entities.Units;
        if (slot < 0 || slot >= pool.End || !pool.Storage[slot].Token.Occupied)
        {
            return false;
        }

        ref readonly var unit = ref pool.Storage[slot];
        return world.Map.Geometry.InBounds(unit.Position) && unit.Owner.Value == world.Globals.TurnCiv
            && unit.Orders is not (UnitOrder.Sleeping or UnitOrder.Fortified)
            && UnitMovement.Left(world, rules, unit) != 0 && !unit.Flags.SpyMission;
    }

    /// <summary>U04's distinct awake predicate: only negative x, Sleep and spent movement exclude a live unit.
    /// Owner, y, upper bounds, Fortified and spy mission do not add exclusions.</summary>
    public static bool AwakeWithMoves(GameState world, RuleTables rules, int slot)
    {
        var pool = world.Entities.Units;
        if (slot < 0 || slot >= pool.End || !pool.Storage[slot].Token.Occupied)
        {
            return false;
        }

        ref readonly var unit = ref pool.Storage[slot];
        return unit.Position.X >= 0 && unit.Orders != UnitOrder.Sleeping && UnitMovement.Left(world, rules, unit) != 0;
    }

    /// <summary>U04 three-pass search with active-unit ties and global Waiting clears. The caller supplies
    /// its semantic map cursor and local observer; no rendering state is read by the engine.</summary>
    public static UnitSlot? NextSelectable(
        GameState world, RuleTables rules, UnitSlot? previous, MapPoint cursor, int searchMode, int localObserver)
    {
        var pool = world.Entities.Units;
        int bestScore = 9999;
        UnitSlot? selected = null, protectedUnit = null;
        MapPoint origin = cursor;
        if (previous is { } prior && pool.Live(prior) && world.Globals.TurnCiv != 0)
        {
            origin = pool.At(prior).Position;
            protectedUnit = world.Globals.ActiveUnit;
        }

        for (int pass = 0; pass < 3; ++pass)
        {
            for (int i = 0; i < pool.End; ++i)
            {
                ref readonly var unit = ref pool.Storage[i];
                if (!Selectable(world, rules, i) || unit.Flags.Waiting
                    || searchMode == 1 && unit.Owner.Value != localObserver)
                {
                    continue;
                }

                int distance = world.Map.Geometry.WeightedDistance(
                    origin, unit.Position, world.Globals.Rules.FlatWorld);
                int score = unchecked(2 * distance + (rules.Units[unit.Type.Value].Domain == RuleDomain.Sea ? 1 : 0));
                if (score < bestScore || score == bestScore && world.Globals.ActiveUnit?.Value == i)
                {
                    bestScore = score;
                    selected = new((ushort)i);
                }
            }

            if (selected is not null)
            {
                return selected;
            }

            for (int i = 0; i < pool.End; ++i)
            {
                if (pool.Storage[i].Token.Occupied && (pass != 0 || protectedUnit?.Value != i))
                {
                    pool.Storage[i].Flags.Waiting = false;
                }
            }
        }

        return null;
    }
}
