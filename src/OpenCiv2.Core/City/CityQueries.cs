using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.City;

/// <summary>C01 city queries over engine state. Player disclosure belongs to the scoped projection.</summary>
public static class CityQueries
{
    /// <summary>C01 exact lookup: the tile city-owner gate precedes the first live exact-coordinate match.</summary>
    public static CitySlot? At(GameState world, MapPoint position)
    {
        if (!world.Map.Geometry.InBounds(position) || world.Map.CityOwner(position) is null)
        {
            return null;
        }

        var pool = world.Entities.Cities;
        for (int i = 0; i < pool.End; ++i)
        {
            ref readonly var city = ref pool.Storage[i];
            if (city.Token.Occupied && city.Position == position)
            {
                return new((ushort)i);
            }
        }

        return null;
    }

    /// <summary>C01 weighted nearest city: owner is reduced to a byte, alternate owner is compared in full,
    /// later equal distances win, and absence retains distance 9999. The region gate tests the origin's
    /// region, not each candidate's. Requested region -2 requires a Port Facility.</summary>
    public static (CitySlot? Slot, int Distance) Nearest(
        GameState world, MapPoint origin, int owner = -1, int alternateOwner = -1, int requestedRegion = -1)
    {
        CitySlot? selected = null;
        int distance = 9999;
        byte? originRegion = requestedRegion >= 0 ? world.Map.LandRegion(origin) : null;
        var pool = world.Entities.Cities;
        for (int i = 0; i < pool.End; ++i)
        {
            ref readonly var city = ref pool.Storage[i];
            if (!city.Token.Occupied || originRegion is { } region && region != requestedRegion
                || owner >= 0 && city.Owner.Value != unchecked((byte)owner)
                    && (alternateOwner < 0 || city.Owner.Value != alternateOwner)
                || requestedRegion == -2 && !BuildingQueries.HasBuilding(world, new((ushort)i), BuildingIds.PortFacility))
            {
                continue;
            }

            int candidate = world.Map.Geometry.WeightedDistance(origin, city.Position, world.Globals.Rules.FlatWorld);
            if (candidate <= distance)
            {
                distance = candidate;
                selected = new((ushort)i);
            }
        }

        return (selected, distance);
    }

    public static bool TouchesBody(GameState world, CitySlot slot, int region)
    {
        if (region >= 63)
        {
            return false;
        }

        var center = world.Entities.Cities.At(slot).Position;
        foreach (var offset in MapNeighborhoods.Directions)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, center, offset, world.Globals.Rules.FlatWorld);
            if (world.Map.Geometry.InBounds(point) && world.Map.TileAt(point).Ocean && world.Map.TileAt(point).Region == region)
            {
                return true;
            }
        }
        return false;
    }

    public static bool ShareOcean(GameState world, CitySlot first, CitySlot second)
    {
        var center = world.Entities.Cities.At(first).Position;
        int previous = -1;
        foreach (var offset in MapNeighborhoods.Directions)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, center, offset, world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point) || !world.Map.TileAt(point).Ocean)
            {
                continue;
            }

            int region = world.Map.TileAt(point).Region;
            if (region == previous)
            {
                continue;
            }

            if (TouchesBody(world, second, region))
            {
                return true;
            }

            previous = region;
        }
        return false;
    }

    /// <summary>C01's skipping traversal returns the last visited sea, its maximum size, and final direction.
    /// The returned region need not have that maximum size; the final direction can exceed eight.</summary>
    public static (int LastRegion, int Maximum, int TraversalIndex) AdjacentSea(GameState world, CitySlot slot)
    {
        var center = world.Entities.Cities.At(slot).Position;
        int direction = 0, maximum = 0, last = 0;
        while (direction < MapNeighborhoods.Directions.Length)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, center, MapNeighborhoods.Directions[direction], world.Globals.Rules.FlatWorld);
            if (world.Map.Geometry.InBounds(point) && world.Map.TileAt(point).Ocean)
            {
                last = world.Map.TileAt(point).Region;
                int size = world.Map.Regions[last].Water;
                if (last >= 63)
                {
                    size = 1;
                }

                if (maximum <= size)
                {
                    maximum = size;
                }

                direction += 2 - (direction & 1);
            }
            ++direction;
        }
        return (last, maximum, direction);
    }
}
