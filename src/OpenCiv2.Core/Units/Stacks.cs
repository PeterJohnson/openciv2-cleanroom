using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum StackProblem { EntryRange, Infinite, BackwardDead, ForwardDead, BackwardX, BackwardY, ForwardX, ForwardY }
public readonly record struct StackDiagnostic(StackProblem Problem, int Current, int Neighbor = -1);
public enum StackStop { Steps, ValidationDepth }
public sealed class StackResourceStop(StackStop reason) : Exception(
    reason == StackStop.Steps ? "Unit stack execution budget exhausted" : "Unit stack validation depth exhausted")
{
    public StackStop Reason { get; } = reason;
}
/// <summary>Shared host resource limits; exhaustion is a failure retaining earlier effects, never a gameplay
/// result.</summary>
public sealed class StackBudget(ulong steps, uint validationDepth)
{
    public ulong Steps = steps;
    public uint ValidationDepth = validationDepth;
}

/// <summary>U01 §§2–4: bounded traversal, repair and local placement. Caller establishes local/host
/// authority.</summary>
public sealed class LocalStacks
{
    private readonly UnitPool units;
    private readonly MapStorage map;
    private readonly UnitStackState retained;
    private readonly StackBudget budget;
    private readonly List<StackDiagnostic>? diagnostics;
    private readonly Action<int>? repairDetach;
    private readonly Action<int, MapPoint>? repairAttach;
    private uint depth;

    public LocalStacks(
        UnitPool units,
        MapStorage map,
        UnitStackState retained,
        StackBudget budget,
        List<StackDiagnostic>? diagnostics = null)
        : this(units, map, retained, budget, diagnostics, null, null) { }

    internal LocalStacks(
        UnitPool units,
        MapStorage map,
        UnitStackState retained,
        StackBudget budget,
        List<StackDiagnostic>? diagnostics,
        Action<int>? repairDetach,
        Action<int, MapPoint>? repairAttach)
    {
        this.units = units;
        this.map = map;
        this.retained = retained;
        this.budget = budget;
        this.diagnostics = diagnostics;
        this.repairDetach = repairDetach;
        this.repairAttach = repairAttach;
    }

    public ref Unit BackedUnit(int slot)
    {
        if (slot < 0 || slot >= units.Storage.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Unit stack index is outside the owned pool");
        }

        return ref units.Storage[slot];
    }

    internal void Step()
    {
        if (budget.Steps == 0)
        {
            throw new StackResourceStop(StackStop.Steps);
        }

        --budget.Steps;
    }

    public int RawForwardCount(int slot)
    {
        int count = 0;
        while (slot >= 0 && count < EntityLimits.Units)
        {
            Step();
            ++count;
            slot = (BackedUnit(slot).Next?.Value ?? -1);
        }
        return count;
    }

    private void Cut(int slot, int neighbor, bool backward)
    {
        if (backward)
        {
            BackedUnit(slot).Previous = null;
            if ((BackedUnit(neighbor).Next?.Value ?? -1) == slot)
            {
                BackedUnit(neighbor).Next = null;
            }
        }
        else
        {
            BackedUnit(slot).Next = null;
            if ((BackedUnit(neighbor).Previous?.Value ?? -1) == slot)
            {
                BackedUnit(neighbor).Previous = null;
            }
        }
    }

    public int Validate(int slot)
    {
        Step();
        if (depth >= budget.ValidationDepth)
        {
            throw new StackResourceStop(StackStop.ValidationDepth);
        }

        ++depth;
        try
        {
            if (slot < 0 || slot >= units.Storage.Length)
            {
                diagnostics?.Add(new(StackProblem.EntryRange, slot));
                throw new ArgumentOutOfRangeException(nameof(slot), "Stack validation index is outside the owned pool");
            }
            if (retained.Suppression != 0)
            {
                return 1;
            }

            int result = 1;
            if (RawForwardCount(slot) >= EntityLimits.Units)
            {
                diagnostics?.Add(new(StackProblem.Infinite, slot));
                result = 0;
                int current = slot, savedNext = (BackedUnit(current).Next?.Value ?? -1);
                do
                {
                    if (current < 0)
                    {
                        break;
                    }

                    Step();
                    var position = BackedUnit(current).Position;
                    if (repairDetach is null)
                    {
                        Detach(current);
                        AttachAtFront(current, position);
                    }
                    else
                    {
                        repairDetach(current);
                        repairAttach!(current, position);
                    }
                    current = savedNext;
                    if (current < 0)
                    {
                        break; // U01 terminal-read repair: no fabricated negative record.
                    }

                    savedNext = (BackedUnit(current).Next?.Value ?? -1);
                } while (current != slot);
            }
            for (int pass = 0; pass < 4; ++pass)
            {
                bool backward = pass % 2 == 0;
                int current = slot;
                for (; ; )
                {
                    Step();
                    int neighbor = backward
                        ? BackedUnit(current).Previous?.Value ?? -1
                        : BackedUnit(current).Next?.Value ?? -1;
                    if (neighbor < 0)
                    {
                        break;
                    }

                    StackProblem? problem = null;
                    if (pass == 0)
                    {
                        if (!BackedUnit(neighbor).Token.Occupied)
                        {
                            problem = StackProblem.BackwardDead;
                        }
                    }
                    else if (pass == 1)
                    {
                        if (!BackedUnit(current).Token.Occupied)
                        {
                            problem = StackProblem.ForwardDead;
                        }
                    }
                    else if (BackedUnit(neighbor).Position.X != BackedUnit(current).Position.X)
                    {
                        problem = backward ? StackProblem.BackwardX : StackProblem.ForwardX;
                    }
                    else if (BackedUnit(neighbor).Position.Y != BackedUnit(current).Position.Y)
                    {
                        problem = backward ? StackProblem.BackwardY : StackProblem.ForwardY;
                    }

                    if (problem is not null)
                    {
                        diagnostics?.Add(new(problem.Value, current, neighbor));
                        result = 0;
                        Cut(current, neighbor, backward);
                    }
                    current = neighbor;
                }
            }
            return result;
        }
        finally
        {
            --depth;
        }
    }

    public int Next(int slot)
    {
        if (slot < 0)
        {
            return slot;
        }

        Validate(slot);
        return (BackedUnit(slot).Next?.Value ?? -1);
    }

    private int EndOfChain(int slot, bool backward)
    {
        if (slot < 0)
        {
            return slot;
        }

        Validate(slot);
        for (; ; )
        {
            Step();
            int neighbor = backward
                ? BackedUnit(slot).Previous?.Value ?? -1
                : BackedUnit(slot).Next?.Value ?? -1;
            if (neighbor < 0 || neighbor == slot)
            {
                return slot;
            }

            slot = neighbor;
        }
    }

    public int First(int slot) => EndOfChain(slot, true);

    public int Last(int slot) => EndOfChain(slot, false);

    private static int SignedOwner(in Unit unit) => unchecked((sbyte)unit.Owner.Value);

    public int FirstForOwnerAt(int owner, MapPoint position)
    {
        for (int i = 0; i < units.End; ++i)
        {
            Step();
            ref var unit = ref units.Storage[i];
            if (unit.Token.Occupied && SignedOwner(unit) == owner && unit.Position == position)
            {
                return First(i);
            }
        }
        return -1;
    }

    public int FirstAt(MapPoint position)
    {
        if (retained.AnimationActive && map.UnitOwner(position) is null)
        {
            return -1;
        }

        for (int i = 0; i < units.End; ++i)
        {
            Step();
            ref var unit = ref units.Storage[i];
            if (unit.Token.Occupied && unit.Position == position)
            {
                Validate(i);
                return First(i);
            }
        }
        return -1;
    }

    public void Detach(int slot)
    {
        Step();
        retained.DetachActive = true;
        DetachBody(slot);
        retained.DetachActive = false;
    }

    internal void DetachBody(int slot)
    {
        bool previous = BackedUnit(slot).Previous is not null;
        if (BackedUnit(slot).Previous is { } previousSlot)
        {
            BackedUnit(previousSlot.Value).Next = BackedUnit(slot).Next;
        }

        bool next = BackedUnit(slot).Next is not null;
        if (BackedUnit(slot).Next is { } nextSlot)
        {
            BackedUnit(nextSlot.Value).Previous = BackedUnit(slot).Previous;
        }

        BackedUnit(slot).Previous = null;
        BackedUnit(slot).Next = null;
        var position = BackedUnit(slot).Position;
        if (!previous && !next && map.Geometry.InBounds(position))
        {
            map.Mutate(new TileChange.Features(position, MapFeatures.Unit, false));
        }

        short staging = unchecked((short)(-100 * (SignedOwner(BackedUnit(slot)) + 1)));
        BackedUnit(slot).Position = new(staging, staging);
    }

    public void AttachAtFront(int slot, MapPoint position)
    {
        Step();
        retained.AttachActive = true;
        AttachBody(slot, position);
        retained.AttachActive = false;
    }

    internal void AttachBody(int slot, MapPoint position)
    {
        int head = FirstForOwnerAt(SignedOwner(BackedUnit(slot)), position);
        BackedUnit(slot).Position = new(unchecked((short)position.X), unchecked((short)position.Y));
        BackedUnit(slot).Previous = null;
        BackedUnit(slot).Next = head < 0 ? null : new UnitSlot((ushort)head);
        if (head >= 0)
        {
            BackedUnit(head).Previous = new UnitSlot((ushort)slot);
        }
        else if (map.Geometry.InBounds(position))
        {
            map.Mutate(new TileChange.Features(position, MapFeatures.Unit));
            int owner = SignedOwner(BackedUnit(slot));
            map.Mutate(new TileChange.Owner(position, owner is >= 0 and <= EntityLimits.Civilizations ? (byte)owner : null));
        }
    }
}
