using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>C01 local radius claims and fertility suppression. Claims are independent of tile occupants.</summary>
public static class CityArea
{
    public static void Refresh(GameState world, CitySlot slot)
    {
        var city = world.Entities.Cities.At(slot);
        Refresh(world, city.Position, city.Owner);
    }

    internal static void Refresh(GameState world, MapPoint center, CivilizationSlot owner)
    {
        MapKnowledge.RequireLocal(world);
        for (int i = 0; i < MapNeighborhoods.Radius.Length; ++i)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, center, MapNeighborhoods.Radius[i], world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point))
            {
                continue;
            }

            ref var tile = ref world.Map.TileAt(point);
            tile.Fertility %= 8;
            if (i <= 20)
            {
                tile.Claimant = owner.Value;
            }
        }
    }
}
