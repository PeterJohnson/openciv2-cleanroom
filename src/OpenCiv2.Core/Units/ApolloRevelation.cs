using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>W03 §8 locally authorized Apollo publication. It updates exploration, feature memory
/// and city knowledge for all seats without revealing units or enabling a map cheat.</summary>
public static class ApolloRevelation
{
    public static void Run(GameState world, Action redrawMaps, Action astronauts)
    {
        MapKnowledge.RequireLocal(world);
        var everyone = default(CivilizationSet);
        for (int c = 0; c < EntityLimits.Civilizations; ++c)
        {
            everyone = everyone.With(c);
        }

        for (int i = 0; i < world.Map.Geometry.Area; ++i)
        {
            world.Map.Tiles[i].RevealedTo = everyone;
            for (int c = 1; c < EntityLimits.Civilizations; ++c)
            {
                world.Map.RememberedLayer(c)[i] = world.Map.Tiles[i].Features;
            }
        }
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (!city.Token.Occupied)
            {
                continue;
            }

            city.KnownTo = everyone;
            for (int c = 0; c < EntityLimits.Civilizations; ++c)
            {
                city.RememberedSize[c] = unchecked((byte)city.Size);
            }

            world.Map.TileAt(city.Position).RevealedTo = everyone;
        }
        redrawMaps();
        if (!world.Multiplayer.Saved.AutoplayActive)
        {
            astronauts();
        }
    }
}
