using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U02 adjacent unit-owner queries. The returned owner is the query's scratch result;
/// city exemption belongs only to InEnemyZone.</summary>
public static class SpatialControl
{
    public static int? AdjacentNonalliedUnit(GameState world, MapPoint origin, int owner, bool sameMedium = false)
    {
        bool ocean = world.Map.TileAt(origin).Ocean;
        foreach (var offset in MapNeighborhoods.Directions)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, origin, offset, world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point) || sameMedium && world.Map.TileAt(point).Ocean != ocean)
            {
                continue;
            }

            if (world.Map.UnitOwner(point) is byte other && other != owner && !world.Civilizations[owner].Treaties[other].Alliance)
            {
                return other;
            }
        }
        return null;
    }

    public static int? InEnemyZone(GameState world, MapPoint origin, int owner) =>
        world.Map.CityOwner(origin) is null ? AdjacentNonalliedUnit(world, origin, owner, true) : null;
}
