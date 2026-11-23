using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum PlacementOperation { Detach, Attach, Relocate, Bottom }
/// <summary>U01 §§2–4,9 local/host placement. Session owns remote requests and synchronization scheduling.</summary>
public sealed class StackOperations
{
    private readonly GameState world;
    private readonly Action? synchronization;
    private readonly LocalStacks local;

    public StackOperations(
        GameState world,
        StackBudget budget,
        List<StackDiagnostic>? diagnostics = null,
        Action? synchronization = null)
    {
        this.world = world;
        this.synchronization = synchronization;
        local = new(
            world.Entities.Units,
            world.Map,
            world.UnitStacks,
            budget,
            diagnostics,
            s => Detach(s, false),
            (s, p) => AttachAtFront(s, p, false));
    }

    public int Validate(int slot) => local.Validate(slot);

    public int RawForwardCount(int slot) => local.RawForwardCount(slot);

    public int Next(int slot) => local.Next(slot);

    public int First(int slot) => local.First(slot);

    public int Last(int slot) => local.Last(slot);

    public int FirstForOwnerAt(int owner, MapPoint position) => local.FirstForOwnerAt(owner, position);

    public int FirstAt(MapPoint position) => local.FirstAt(position);

    /// <summary>U01 count aggregate, including validated traversal and the sum cutoff.</summary>
    public int Count(int slot)
    {
        int count = 0;
        for (int current = First(slot); current >= 0 && count < EntityLimits.Units; current = Next(current))
        {
            local.Step();
            ++count;
        }
        return count;
    }

    /// <summary>U01 §2: signed role equality across the complete validated chain, without aggregate sum cutoff.</summary>
    public int CountRole(int slot, int role, RuleTables rules)
    {
        int count = 0;
        for (int current = First(slot); current >= 0; current = Next(current))
        {
            local.Step();
            if (unchecked((sbyte)rules.Units[local.BackedUnit(current).Type.Value].Role) == role)
            {
                count = unchecked(count + 1);
            }
        }
        return count;
    }

    /// <summary>U01 stack publication excludes each member's own owner and preserves prior knowledge.</summary>
    public bool MarkSeen(int slot, int observer)
    {
        bool changed = false;
        for (int current = First(slot); current >= 0; current = Next(current))
        {
            local.Step();
            ref var unit = ref local.BackedUnit(current);
            if (observer >= 0 && unchecked((sbyte)unit.Owner.Value) != observer)
            {
                changed |= !unit.Visibility.Contains(observer);
                unit.Visibility = unit.Visibility.With(observer);
            }
        }
        return changed;
    }

    /// <summary>U01 §9 whole-stack clear affects unit seen-by sets only, using validated traversal.</summary>
    public void ClearVisibility(int slot)
    {
        for (int current = First(slot); current >= 0; current = Next(current))
        {
            local.Step();
            local.BackedUnit(current).Visibility = default;
        }
    }

    private ref bool Active(PlacementOperation operation)
    {
        switch (operation)
        {
            case PlacementOperation.Detach:
                return ref world.UnitStacks.DetachActive;
            case PlacementOperation.Attach:
                return ref world.UnitStacks.AttachActive;
            case PlacementOperation.Relocate:
                return ref world.UnitStacks.RelocateActive;
            case PlacementOperation.Bottom:
                return ref world.UnitStacks.BottomActive;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private void Begin(PlacementOperation operation)
    {
        // This is a local engine API. Session's command boundary owns the remote busy/request path.
        if (world.Globals.Multiplayer >= GameMode.Tcp && !world.Multiplayer.NetworkHost)
        {
            throw new InvalidOperationException(
                "Local placement requires authority; route through the session unit operation");
        }

        Active(operation) = true;
    }

    private void Synchronize(bool sync)
    {
        if (world.Globals.Multiplayer >= GameMode.Tcp && sync)
        {
            (synchronization
                ?? throw new InvalidOperationException("Placement synchronization requires its session service"))();
        }
    }

    private void Finish(PlacementOperation operation, bool sync)
    {
        Synchronize(sync);
        Active(operation) = false;
    }

    public void Detach(int slot, bool sync)
    {
        local.Step();
        Begin(PlacementOperation.Detach);
        local.DetachBody(slot);
        Finish(PlacementOperation.Detach, sync);
    }

    public void AttachAtFront(int slot, MapPoint position, bool sync)
    {
        local.Step();
        Begin(PlacementOperation.Attach);
        local.AttachBody(slot, position);
        Finish(PlacementOperation.Attach, sync);
    }

    public void Relocate(int slot, MapPoint position, bool sync)
    {
        local.Step();
        Begin(PlacementOperation.Relocate);
        Detach(slot, false);
        AttachAtFront(slot, position, false);
        Finish(PlacementOperation.Relocate, sync);
    }

    public void RelocateInPlace(int slot, bool sync) =>
        Relocate(slot, local.BackedUnit(slot).Position, sync);

    public void MoveToBottom(int slot, bool sync)
    {
        if (local.BackedUnit(slot).Next is null)
        {
            return;
        }

        local.Step();
        Begin(PlacementOperation.Bottom);
        if (local.BackedUnit(slot).Next is not null)
        {
            var position = new MapPoint(local.BackedUnit(slot).Position.X, local.BackedUnit(slot).Position.Y);
            int anchor = First(slot);
            if (anchor == slot)
            {
                anchor = Next(slot);
            }

            Detach(slot, false);
            int tail = Last(anchor);
            local.BackedUnit(tail).Next = slot < 0 ? null : new UnitSlot((ushort)slot);
            local.BackedUnit(slot).Previous = tail < 0 ? null : new UnitSlot((ushort)tail);
            local.BackedUnit(slot).Next = null;
            local.BackedUnit(slot).Position = new(unchecked((short)position.X), unchecked((short)position.Y));
        }
        Finish(PlacementOperation.Bottom, sync);
    }

    public void RelocateStack(int slot, MapPoint position, bool sync)
    {
        int current = First(slot);
        while (current >= 0)
        {
            local.Step();
            int savedNext = Next(current);
            Relocate(current, position, false);
            current = savedNext;
        }
        Synchronize(sync);
    }

    /// <summary>U01 §4: two relocation passes preserve ship order, including preexisting staging groups.</summary>
    public void ShipsFirst(int slot, RuleTables rules, bool sync)
    {
        if (slot < 0)
        {
            return;
        }
        var position = local.BackedUnit(slot).Position;
        int lastShip = -1;
        for (int current = First(slot); current >= 0;)
        {
            int next = Next(current);
            ref var unit = ref local.BackedUnit(current);
            if (rules.Units[unit.Type.Value].Domain == RuleDomain.Sea)
            {
                Relocate(current, UnitStaging.Ships(unchecked((sbyte)unit.Owner.Value)), sync);
                lastShip = current;
            }
            current = next;
        }
        if (lastShip >= 0)
        {
            for (int current = First(lastShip); current >= 0;)
            {
                int next = Next(current);
                Relocate(current, position, sync);
                current = next;
            }
        }
    }
}
