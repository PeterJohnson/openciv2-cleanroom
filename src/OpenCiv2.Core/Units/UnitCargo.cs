using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U01 Appendix A: distinct off-map coordinate groups, retained within a game.</summary>
public static class UnitStaging
{
    public static MapPoint Lifted(int owner) => Position(owner, -200);
    public static MapPoint Ships(int owner) => Position(owner, -300);
    private static MapPoint Position(int owner, int factor)
    {
        short value = unchecked((short)(factor * (owner + 1)));
        return new(value, value);
    }
}

/// <summary>U01 §7 authoritative cargo grouping. Movement/transport callers own boarding admission.</summary>
public sealed class UnitCargo(GameState world, RuleTables rules, StackOperations stacks, Action? synchronize = null)
{
    private ref Unit Unit(int slot) => ref world.Entities.Units.Storage[slot];
    private int Owner(int slot) => unchecked((sbyte)Unit(slot).Owner.Value);
    private static int Carrier(in Unit unit) => unit.Carrier is { } value ? unchecked((short)value.Value) : -1;

    private void Authority()
    {
        if (world.Globals.Multiplayer >= GameMode.Tcp && !world.Multiplayer.NetworkHost)
        {
            throw new InvalidOperationException("Local cargo operations require authority; route through the session unit operation");
        }
    }

    private void Synchronize(bool sync)
    {
        if (sync && world.Globals.Multiplayer >= GameMode.Tcp)
        {
            (synchronize ?? throw new InvalidOperationException("Cargo synchronization requires its session service"))();
        }
    }

    public int Gather(int ship, bool lift, bool sync)
    {
        Authority();
        world.UnitStacks.CargoActive = true;
        if (lift)
        {
            stacks.ShipsFirst(ship, rules, false);
        }
        int start = stacks.First(ship);
        if (start == ship)
        {
            start = stacks.Next(ship);
        }
        var position = Unit(ship).Position;
        bool inBounds = world.Map.Geometry.InBounds(position);
        bool ocean = inBounds && world.Map.TileAt(position).Ocean;
        if (lift)
        {
            stacks.Relocate(ship, UnitStaging.Lifted(Owner(ship)), false);
        }
        else
        {
            for (int current = start; current >= 0; current = stacks.Next(current))
            {
                Unit(current).Flags.Aboard = false;
            }
        }
        var shipRule = rules.Units[Unit(ship).Type.Value];
        int remaining = unchecked((sbyte)shipRule.Capacity);
        bool carrier = shipRule.Abilities.CarryAircraft;
        bool missile = remaining == 0 && !carrier && shipRule.Abilities.Submarine;
        if (carrier || missile || Owner(ship) == 0)
        {
            remaining = 20;
        }
        bool airCargo = carrier || missile;
        for (int pass = 0; pass < (airCargo ? 1 : 2) && remaining != 0; ++pass)
        {
            for (int current = start; current >= 0 && remaining != 0;)
            {
                int next = stacks.Next(current);
                ref var unit = ref Unit(current);
                var rule = rules.Units[unit.Type.Value];
                bool eligible = rule.Domain == (airCargo ? RuleDomain.Air : RuleDomain.Land);
                if (eligible && missile && !rule.Abilities.DestroyedAfterAttack)
                {
                    eligible = false;
                }
                if (eligible && !airCargo)
                {
                    if (pass == 0)
                    {
                        eligible = unit.Orders == UnitOrder.Sleeping && Carrier(unit) == ship;
                    }
                    else if (!world.Globals.HumanPlayers.Contains(Owner(current)))
                    {
                        eligible = unit.Orders is not (UnitOrder.Fortifying or UnitOrder.Fortified) || ocean;
                        if (eligible && inBounds && !ocean)
                        {
                            eligible = world.Civilizations[Owner(current)].ContinentPolicy[world.Map.TileAt(position).Region] != 1
                                || unchecked((sbyte)rule.Role) > 4;
                        }
                    }
                    else
                    {
                        eligible = unit.Orders == UnitOrder.Sleeping ? Carrier(unit) < 0 : ocean;
                    }
                }
                if (eligible && (lift || !unit.Flags.Aboard))
                {
                    if (current == start)
                    {
                        start = next;
                    }
                    if (airCargo)
                    {
                        if (unit.Orders != UnitOrder.Sleeping)
                        {
                            unit.Orders = UnitOrder.None;
                        }
                    }
                    else
                    {
                        UnitOrders.SetSleep(ref unit);
                        unit.Carrier = new((ushort)ship);
                        unit.Destination = unit.Destination with { X = unchecked((short)ship) };
                    }
                    if (lift)
                    {
                        stacks.Relocate(current, UnitStaging.Lifted(Owner(current)), false);
                    }
                    else
                    {
                        unit.Flags.Aboard = true;
                    }
                    remaining = unchecked(remaining - 1);
                }
                current = next;
            }
        }
        Synchronize(sync);
        world.UnitStacks.CargoActive = false;
        return remaining;
    }

    public void Lift(int slot, bool sync)
    {
        if (slot < 0)
        {
            return;
        }
        Authority();
        world.UnitStacks.LiftActive = true;
        if (rules.Units[Unit(slot).Type.Value].Domain == RuleDomain.Sea)
        {
            Gather(slot, true, false);
        }
        else
        {
            stacks.Relocate(slot, UnitStaging.Lifted(Owner(slot)), false);
        }
        Synchronize(sync);
        world.UnitStacks.LiftActive = false;
    }

    public int MaximumRemaining(int slot, bool sync)
    {
        Authority();
        stacks.ShipsFirst(slot, rules, false);
        int maximum = 0;
        for (int current = stacks.First(slot); current >= 0; current = stacks.Next(current))
        {
            if (rules.Units[Unit(current).Type.Value].Domain == RuleDomain.Sea)
            {
                maximum = Math.Max(maximum, Gather(current, false, false));
            }
        }
        Synchronize(sync);
        return maximum;
    }
}
