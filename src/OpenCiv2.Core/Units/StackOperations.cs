using OpenCiv2.Base;
using OpenCiv2.Contracts;
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

    public int FirstForOwnerAt(int owner, Position position) => local.FirstForOwnerAt(owner, position);

    public int FirstAt(Position position) => local.FirstAt(position);

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

    public void AttachAtFront(int slot, Position position, bool sync)
    {
        local.Step();
        Begin(PlacementOperation.Attach);
        local.AttachBody(slot, position);
        Finish(PlacementOperation.Attach, sync);
    }

    public void Relocate(int slot, Position position, bool sync)
    {
        local.Step();
        Begin(PlacementOperation.Relocate);
        Detach(slot, false);
        AttachAtFront(slot, position, false);
        Finish(PlacementOperation.Relocate, sync);
    }

    public void RelocateInPlace(int slot, bool sync) =>
        Relocate(slot, new(local.BackedUnit(slot).X, local.BackedUnit(slot).Y), sync);

    public void MoveToBottom(int slot, bool sync)
    {
        if (local.BackedUnit(slot).Next.Value < 0)
        {
            return;
        }

        local.Step();
        Begin(PlacementOperation.Bottom);
        if (local.BackedUnit(slot).Next.Value >= 0)
        {
            var position = new Position(local.BackedUnit(slot).X, local.BackedUnit(slot).Y);
            int anchor = First(slot);
            if (anchor == slot)
            {
                anchor = Next(slot);
            }

            Detach(slot, false);
            int tail = Last(anchor);
            local.BackedUnit(tail).Next = new(unchecked((short)slot));
            local.BackedUnit(slot).Previous = new(unchecked((short)tail));
            local.BackedUnit(slot).Next = new(-1);
            local.BackedUnit(slot).X = unchecked((short)position.X);
            local.BackedUnit(slot).Y = unchecked((short)position.Y);
        }
        Finish(PlacementOperation.Bottom, sync);
    }

    public void RelocateStack(int slot, Position position, bool sync)
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
}
