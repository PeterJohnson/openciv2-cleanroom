using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U02 local Cleanup mutations and full result. The dispatcher owns operation lifetime;
/// network synchronization/unlock belong to the deferred authority path.</summary>
public static class MovementCleanup
{
    public static int Apply(GameState world, RuleTables rules, MovementContinuation movement, int capturedOwner, int result)
    {
        MapKnowledge.RequireLocal(world);
        if (world.Entities.Units.End != movement.UnitBound)
        { return 0; }
        ref var unit = ref world.Entities.Units.At(movement.Unit);
        if (result == 0)
        {
            unit.Orders = UnitOrder.None;
            if (unit.Flags.Automated)
            { UnitMovement.Exhaust(world, rules, ref unit); }
            if (rules.Units[unit.Type.Value].Role == RuleRole.SeaTransport)
            { unit.Counter = unchecked((byte)(world.Globals.Turn % 8)); }
            if (!world.Globals.HumanPlayers.Contains(capturedOwner))
            {
                if (unit.Flags.Garrison)
                { unit.Orders = UnitOrder.Fortified; }
                unit.AuxiliaryTimer = unchecked((byte)(unit.AuxiliaryTimer + 1));
                if (unchecked((sbyte)unit.AuxiliaryTimer) >= 20)
                { unit.AuxiliaryTimer = 0; UnitMovement.Exhaust(world, rules, ref unit); }
            }
        }
        else
        { unit.Flags.Garrison = false; }
        return result;
    }
}
