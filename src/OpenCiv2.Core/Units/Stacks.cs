using OpenCiv2.Base;
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
    private readonly Action<int, Position>? repairAttach;
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
        Action<int, Position>? repairAttach)
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
        while (slot >= 0 && count < 2048)
        {
            Step();
            ++count;
            slot = BackedUnit(slot).Next.Value;
        }
        return count;
    }

    private void Cut(int slot, int neighbor, bool backward)
    {
        if (backward)
        {
            BackedUnit(slot).Previous = new(-1);
            if (BackedUnit(neighbor).Next.Value == slot)
            {
                BackedUnit(neighbor).Next = new(-1);
            }
        }
        else
        {
            BackedUnit(slot).Next = new(-1);
            if (BackedUnit(neighbor).Previous.Value == slot)
            {
                BackedUnit(neighbor).Previous = new(-1);
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
            if (RawForwardCount(slot) >= 2048)
            {
                diagnostics?.Add(new(StackProblem.Infinite, slot));
                result = 0;
                int current = slot, savedNext = BackedUnit(current).Next.Value;
                do
                {
                    if (current < 0)
                    {
                        break;
                    }

                    Step();
                    var position = new Position(BackedUnit(current).X, BackedUnit(current).Y);
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

                    savedNext = BackedUnit(current).Next.Value;
                } while (current != slot);
            }
            for (int pass = 0; pass < 4; ++pass)
            {
                bool backward = pass % 2 == 0;
                int current = slot;
                for (; ; )
                {
                    Step();
                    int neighbor = backward ? BackedUnit(current).Previous.Value : BackedUnit(current).Next.Value;
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
                    else if (BackedUnit(neighbor).X != BackedUnit(current).X)
                    {
                        problem = backward ? StackProblem.BackwardX : StackProblem.ForwardX;
                    }
                    else if (BackedUnit(neighbor).Y != BackedUnit(current).Y)
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
        return BackedUnit(slot).Next.Value;
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
            int neighbor = backward ? BackedUnit(slot).Previous.Value : BackedUnit(slot).Next.Value;
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

    public int FirstForOwnerAt(int owner, Position position)
    {
        for (int i = 0; i < units.End; ++i)
        {
            Step();
            ref var unit = ref units.Storage[i];
            if (unit.Token.Occupied && SignedOwner(unit) == owner && unit.X == position.X && unit.Y == position.Y)
            {
                return First(i);
            }
        }
        return -1;
    }

    public int FirstAt(Position position)
    {
        if (retained.AnimationActive && map.UnitOwner(position) is null)
        {
            return -1;
        }

        for (int i = 0; i < units.End; ++i)
        {
            Step();
            ref var unit = ref units.Storage[i];
            if (unit.Token.Occupied && unit.X == position.X && unit.Y == position.Y)
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
        bool previous = BackedUnit(slot).Previous.Value >= 0;
        if (previous)
        {
            BackedUnit(BackedUnit(slot).Previous.Value).Next = BackedUnit(slot).Next;
        }

        bool next = BackedUnit(slot).Next.Value >= 0;
        if (next)
        {
            BackedUnit(BackedUnit(slot).Next.Value).Previous = BackedUnit(slot).Previous;
        }

        BackedUnit(slot).Previous = new(-1);
        BackedUnit(slot).Next = new(-1);
        var position = new Position(BackedUnit(slot).X, BackedUnit(slot).Y);
        if (!previous && !next && map.Geometry.InBounds(position))
        {
            map.Mutate(new(TileField.Features, position, 1, 0));
        }

        short staging = unchecked((short)(-100 * (SignedOwner(BackedUnit(slot)) + 1)));
        BackedUnit(slot).X = BackedUnit(slot).Y = staging;
    }

    public void AttachAtFront(int slot, Position position)
    {
        Step();
        retained.AttachActive = true;
        AttachBody(slot, position);
        retained.AttachActive = false;
    }

    internal void AttachBody(int slot, Position position)
    {
        int head = FirstForOwnerAt(SignedOwner(BackedUnit(slot)), position);
        BackedUnit(slot).X = unchecked((short)position.X);
        BackedUnit(slot).Y = unchecked((short)position.Y);
        BackedUnit(slot).Previous = new(-1);
        BackedUnit(slot).Next = new(unchecked((short)head));
        if (head >= 0)
        {
            BackedUnit(head).Previous = new(unchecked((short)slot));
        }
        else if (map.Geometry.InBounds(position))
        {
            map.Mutate(new(TileField.Features, position, 1));
            map.Mutate(new(TileField.Owner, position, SignedOwner(BackedUnit(slot))));
        }
    }
}
