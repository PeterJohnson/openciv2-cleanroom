using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record SpaceshipArrivalNotice(int Civilization, bool ShowLanding);

/// <summary>F04 global arrival boundary. One ascending normal slot, without an alive filter or chance draw.</summary>
public static class SpaceshipArrival
{
    public static SpaceshipArrivalNotice? Begin(GameState world)
    {
        MapKnowledge.RequireLocal(world);
        for (int c = 1; c < EntityLimits.Civilizations; ++c)
        {
            ref var ship = ref world.Civilizations[c].SpaceshipFlags;
            if (ship.Arrived || !SpaceshipQueries.Arrived(world, c))
            { continue; }
            ship.Arrived = true;
            if (!world.Globals.Status.Won)
            {
                ship.FirstArrivalVictory = true;
                world.SpaceshipWinner = c;
                world.PendingEndReason = c == world.Multiplayer.LocalHuman ? EndReason.LocalSpace : EndReason.OtherSpace;
            }
            return new(c, !world.Multiplayer.Saved.AutoplayActive
                && (c != world.Multiplayer.LocalHuman || world.PendingEndReason == EndReason.None));
        }
        return null;
    }

    /// <summary>After the optional landing notice; later arrivals retain the original winner and reason.</summary>
    public static void Complete(GameState world) => world.Globals.Status.Won = true;
}
