using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>Required owning children, not optional observers. Failures retain preceding U01 effects.</summary>
public interface IUnitDeletionEffects
{
    void UnitRemoved(MapPoint position);

    void RefreshHomeCity(int city);

    void CheckCivilization(int owner, int killer);

    /// <summary>Session owns state diff, position notice, positive-home notice and flush(5000).</summary>
    void SynchronizeDeletion(MapPoint position, CitySlot? home);
}

/// <summary>U01 §§6,9 local/host basic deletion. Session owns remote lifetimes; V01 owns elimination.</summary>
public sealed class UnitDeletion
{
    private readonly GameState world;
    private readonly RuleTables rules;
    private readonly LocalStacks stacks;
    private readonly IUnitDeletionEffects effects;

    public UnitDeletion(
        GameState world,
        RuleTables rules,
        StackBudget budget,
        IUnitDeletionEffects effects,
        List<StackDiagnostic>? diagnostics = null)
    {
        this.world = world;
        this.rules = rules;
        this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        stacks = new(world.Entities.Units, world.Map, world.UnitStacks, budget, diagnostics);
    }

    private int Role(int type)
    {
        if (type < 0 || type >= rules.Units.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Deletion has no unit-rule backing");
        }

        return unchecked((sbyte)rules.Units[type].Role);
    }

    private ref Civilization Civilization(int owner)
    {
        if (owner < 0 || owner >= world.Civilizations.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(owner), "Deletion has no civilization backing");
        }

        return ref world.Civilizations[owner];
    }

    private ref byte ActiveCount(int owner, int type)
    {
        Span<byte> active = Civilization(owner).UnitsActive;
        if (type < 0 || type >= active.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Deletion has no active-count backing");
        }

        return ref active[type];
    }

    public void Erase(int slot, bool notify)
    {
        if (slot < 0 || !stacks.BackedUnit(slot).Token.Occupied)
        {
            return;
        }

        if (world.Globals.Multiplayer >= GameMode.Tcp && !world.Multiplayer.NetworkHost)
        {
            throw new InvalidOperationException(
                "Local deletion requires authority; route through the session unit operation");
        }

        world.UnitDeletion.Active = true;
        ref var unit = ref stacks.BackedUnit(slot);
        var position = unit.Position;
        int type = unit.Type.Value, owner = unchecked((sbyte)unit.Owner.Value);
        var home = unit.Home;
        if (owner >= 0 && slot < EntityLimits.Units)
        {
            ref var civ = ref Civilization(owner);
            if (civ.MilitaryCount != 0 && Role(type) < 5)
            {
                civ.MilitaryCount = unchecked((short)(civ.MilitaryCount - 1));
            }

            ref byte active = ref ActiveCount(owner, type);
            if (active != 0)
            {
                --active;
            }

            if (world.UnitTotals[owner] != 0)
            {
                world.UnitTotals[owner] = unchecked(world.UnitTotals[owner] - 1);
            }
        }
        stacks.Detach(slot);
        stacks.BackedUnit(slot).Token = new(0);
        var pool = world.Entities.Units;
        if (slot == pool.End - 1)
        {
            pool.SetEnd(pool.End - 1);
            world.Globals.UnitEnd = (short)pool.End;
        }
        foreach (ref var candidate in pool.Scanned)
        {
            if (candidate.Orders == UnitOrder.Sleeping && candidate.Carrier is { } carrier && carrier.Value == slot)
            {
                // F02 retains the carrier value when waking changes the destination discriminator.
                candidate.Destination = candidate.Destination with { X = carrier.Value };
                candidate.Orders = UnitOrder.None;
            }
        }

        if (notify)
        {
            effects.UnitRemoved(position);
            if (home is CitySlot selected)
            {
                effects.RefreshHomeCity(selected.Value);
            }
        }
        if (Role(type) == 5 && Civilization(owner).CityCount == 0 && ActiveCount(owner, type) == 0)
        {
            effects.CheckCivilization(owner, -1);
        }

        if (world.Globals.Multiplayer >= GameMode.Tcp && notify)
        {
            effects.SynchronizeDeletion(position, home);
        }

        world.UnitDeletion.Active = false;
    }
}
