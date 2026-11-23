using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.City;

/// <summary>V08 §2 objective value. Callers own city admission; raw wonder locations count
/// independently of scenario mode, ownership, activity and expiration.</summary>
public static class CityObjectives
{
    public static int Value(GameState world, CitySlot slot)
    {
        int value = world.Entities.Cities.At(slot).Flags.Objective ? 1 : 0;
        if (world.Scenario.Flags.WonderObjectives)
        {
            for (int w = 0; w < RuleLimits.Wonders; ++w)
            {
                if (world.Globals.WonderCity[w] == slot.Value)
                {
                    ++value;
                }
            }
        }

        return value;
    }
}
