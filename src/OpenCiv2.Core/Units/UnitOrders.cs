using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U04 shared order changes; callers own admission and publication.</summary>
public static class UnitOrders
{
    /// <summary>U01 §7: entering sleep clears only the first destination/carrier field.</summary>
    public static void SetSleep(ref Unit unit)
    {
        if (unit.Orders != UnitOrder.Sleeping)
        {
            unit.Carrier = null;
            unit.Destination = unit.Destination with { X = -1 };
        }
        unit.Orders = UnitOrder.Sleeping;
    }

    public static void SetGoto(ref Unit unit, int task, MapPoint destination)
    {
        unit.Orders = UnitOrder.GoTo;
        unit.AiTask = unchecked((byte)task);
        unit.Destination = new(unchecked((short)destination.X), unchecked((short)destination.Y));
    }

    public static void InterruptGoto(ref Unit unit, RuleTables rules)
    {
        if (((int)unit.Orders & 15) == (int)UnitOrder.GoTo && rules.Units[unit.Type.Value].Role != RuleRole.Trade)
        {
            unit.Orders = UnitOrder.None;
        }
    }

    public static void WakeSentries(GameState world, RuleTables rules, StackOperations stacks, int slot)
    {
        for (int current = stacks.First(slot); current >= 0; current = stacks.Next(current))
        {
            ref var unit = ref world.Entities.Units.Storage[current];
            if (unit.Orders == UnitOrder.Sleeping && (rules.Units[unit.Type.Value].Domain != RuleDomain.Land
                || !world.Map.TileAt(unit.Position).Ocean))
            {
                unit.Orders = UnitOrder.None;
            }
        }
    }

    public static void WakeByRole(GameState world, RuleTables rules, StackOperations stacks, int slot,
        UnitRoleSet roles, bool stopAfterFirst)
    {
        MapKnowledge.RequireLocal(world);
        for (int current = stacks.First(slot); current >= 0; current = stacks.Next(current))
        {
            ref var unit = ref world.Entities.Units.Storage[current];
            if (unit.Orders != UnitOrder.Sleeping || !roles.Contains(unchecked((sbyte)rules.Units[unit.Type.Value].Role)))
            { continue; }
            unit.Orders = UnitOrder.None;
            if (stopAfterFirst)
            { return; }
        }
    }
}
