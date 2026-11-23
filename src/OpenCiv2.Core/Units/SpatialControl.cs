using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U02 adjacent owner queries. The returned owner is the query's scratch result;
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

    /// <summary>A city bridges land/sea control and changes the medium for subsequent neighbors,
    /// even when it belongs to the querying civilization or an ally. City owner precedes unit owner.</summary>
    public static int? AdjacentNonalliedCivilization(GameState world, MapPoint origin, int owner)
    {
        bool medium = world.Map.TileAt(origin).Ocean;
        bool originCity = world.Map.CityOwner(origin) is not null;
        foreach (var offset in MapNeighborhoods.Directions)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, origin, offset, world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point))
            { continue; }
            bool neighborMedium = originCity ? medium : world.Map.TileAt(point).Ocean;
            int? other = world.Map.CityOwner(point);
            if (other is not null)
            { medium = neighborMedium; }
            else
            { other = world.Map.UnitOwner(point); }
            if (other is { } civilization && civilization != owner && neighborMedium == medium
                && !world.Civilizations[owner].Treaties[civilization].Alliance)
            { return civilization; }
        }
        return null;
    }
}
