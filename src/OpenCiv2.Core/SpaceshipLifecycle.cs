using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public readonly record struct SpaceshipLossNotice(int Civilization, string Adjective, bool Returning);

/// <summary>V07 §8 reset/loss children. Construction, launch, race and arrival processing are separate operations.</summary>
public static class SpaceshipLifecycle
{
    public static void Reset(GameState world, int civilization)
    {
        ref var civ = ref world.Civilizations[civilization];
        civ.SpaceshipFlags = default;
        civ.SpaceshipPopulation = civ.SpaceshipLaunchYear = civ.SpaceshipArrivalYear = 0;
        civ.SpaceshipParts = default;
    }

    public static void Lost(GameState world, int civilization, Action<SpaceshipLossNotice> notice)
    {
        MapKnowledge.RequireLocal(world);
        ref var civ = ref world.Civilizations[civilization];
        string adjective = world.Names[civilization].Adjective;
        if (civ.SpaceshipFlags.Launched || civ.SpaceshipFlags.Exists)
        {
            notice(new(civilization, adjective, civ.SpaceshipFlags.Launched));
        }
        Reset(world, civilization);
    }
}
