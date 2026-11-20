using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public static class ClassicLoadedUnits
{
    /// <summary>F06 §11 step 5 only: after successful input and presentation, establish host authority,
    /// repair scanned stacks, then delete negative-coordinate units through real U01 with notify=false.
    /// Required civilization checks remain owning children; the live scan bound can change during them.</summary>
    public static void Cleanup(
        GameState world,
        RuleTables rules,
        StackBudget budget,
        IUnitDeletionEffects effects,
        List<StackDiagnostic>? diagnostics = null,
        Action? placement = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(effects);
        world.Multiplayer.NetworkHost = true;
        var stacks = new StackOperations(world, budget, diagnostics, placement);
        var deletion = new UnitDeletion(world, rules, budget, effects, diagnostics);
        for (int slot = 0; slot < world.Entities.Units.End; ++slot)
        {
            ref var unit = ref world.Entities.Units.Storage[slot];
            if (!unit.Token.Occupied)
            {
                unit.Previous = unit.Next = null;
            }
            else
            {
                stacks.Validate(slot);
                if (unit.Position.X < 0 || unit.Position.Y < 0)
                {
                    deletion.Erase(slot, false);
                }
            }
        }
    }
}
