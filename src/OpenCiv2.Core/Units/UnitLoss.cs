using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public interface IUnitLossEffects
{
    void UnitsChangedAt(MapPoint position);
    void RedrawTile(MapPoint position);
    /// <summary>State differences then flush when redraw is null; otherwise state and draw message, no flush.</summary>
    void SynchronizeLoss(MapPoint? redraw);
}

/// <summary>U01 §6 stack and cargo-aware deletion using the actual grouping and basic-deletion children.</summary>
public sealed class UnitLoss(GameState world, RuleTables rules, StackOperations stacks,
    UnitCargo cargo, UnitDeletion deletion, IUnitLossEffects effects)
{
    private void Authority()
    {
        if (world.Globals.Multiplayer >= GameMode.Tcp && !world.Multiplayer.NetworkHost)
        {
            throw new InvalidOperationException("Local unit loss requires authority; route through the session unit operation");
        }
    }

    private void Synchronize(bool sync, MapPoint? redraw = null)
    {
        if (sync && world.Globals.Multiplayer >= GameMode.Tcp)
        {
            effects.SynchronizeLoss(redraw);
        }
    }

    public void EraseStack(int slot, bool sync)
    {
        foreach (var check in EraseStackSteps(slot, sync))
        { CompleteCheck(check); }
    }

    internal void CompleteCheck(CivilizationCheck check) => deletion.CompleteCheck(check);

    internal IEnumerable<CivilizationCheck> EraseStackSteps(int slot, bool sync)
    {
        Authority();
        for (int current = stacks.First(slot); current >= 0;)
        {
            int next = stacks.Next(current);
            foreach (var check in deletion.EraseSteps(current, false))
            { yield return check; }
            current = next;
        }
        Synchronize(sync);
    }

    public void EraseWithCargo(int slot, bool sync)
    {
        foreach (var check in EraseWithCargoSteps(slot, sync))
        { CompleteCheck(check); }
    }

    private IEnumerable<CivilizationCheck> EraseWithCargoSteps(int slot, bool sync)
    {
        Authority();
        world.UnitDeletion.CargoActive = true;
        ref var unit = ref world.Entities.Units.Storage[slot];
        var position = unit.Position;
        bool atSea = false, handled = false;
        if (rules.Units[unit.Type.Value].Domain == RuleDomain.Sea)
        {
            if (world.Map.Geometry.InBounds(position))
            {
                atSea = world.Map.TileAt(position).Ocean;
            }
            else if (position.X == UnitStaging.Lifted(unchecked((sbyte)unit.Owner.Value)).X)
            {
                foreach (var check in EraseStackSteps(slot, false))
                { yield return check; }
                handled = true;
            }
            else
            {
                atSea = true;
            }
        }
        if (atSea)
        {
            cargo.Gather(slot, false, false);
            foreach (var check in EraseStackSteps(slot, false))
            { yield return check; }
            effects.UnitsChangedAt(position);
        }
        else if (!handled)
        {
            foreach (var check in deletion.EraseSteps(slot, false))
            { yield return check; }
        }
        Synchronize(sync);
        world.UnitDeletion.CargoActive = false;
    }

    public void DestroyAndRedraw(int slot, bool sync)
    {
        foreach (var check in DestroyAndRedrawSteps(slot, sync))
        { CompleteCheck(check); }
    }

    internal IEnumerable<CivilizationCheck> DestroyAndRedrawSteps(int slot, bool sync)
    {
        Authority();
        world.UnitDeletion.DestroyActive = true;
        var position = world.Entities.Units.Storage[slot].Position;
        foreach (var check in EraseWithCargoSteps(slot, false))
        { yield return check; }
        if (world.Map.Geometry.InBounds(position))
        {
            effects.RedrawTile(position);
        }
        Synchronize(sync, position);
        world.UnitDeletion.DestroyActive = false;
    }
}
