using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>W03 exploration and recorded knowledge for ordinary local land units.</summary>
public static class LocalVisibility
{
    private const int Center = 20;

    /// <summary>Copies live features only at a revelation boundary, preserving earlier memory elsewhere.</summary>
    public static bool Remember(GameState world, int observer, MapPoint point)
    {
        ref var tile = ref world.Map.TileAt(point);
        ref var memory = ref world.Map.Remembered(observer, point);
        bool changed = !tile.RevealedTo.Contains(observer) || memory != tile.Features;
        tile.RevealedTo = tile.RevealedTo.With(observer);
        memory = tile.Features;
        return changed;
    }

    /// <summary>Reveals the center, adjacent tiles and same-medium extended positions, in W01 radius order.</summary>
    public static void RevealAround(GameState world, Unit unit, UnitRule rule)
    {
        var map = world.Map;
        int observer = unit.Owner.Value;
        bool flat = world.Globals.Rules.FlatWorld;
        for (int i = 0; i < 25; i++)
        {
            var point = MapNeighborhoods.Offset(map.Geometry, unit.Position, MapNeighborhoods.Radius[i], flat);
            if (map.Geometry.Canonical(point) && (i < MapLimits.Directions || i == Center
                || (rule.Abilities.SeeTwo && !map.TileAt(point).Ocean)))
            {
                Remember(world, observer, point);
            }
        }
    }

    /// <summary>Publishes W03 direct unit or city knowledge that accompanies a denied ZOC step.</summary>
    public static bool RevealAdjacentOccupants(GameState world, int observer, MapPoint origin)
    {
        var map = world.Map;
        bool changed = false;
        foreach (var offset in MapNeighborhoods.Directions)
        {
            var point = MapNeighborhoods.Offset(map.Geometry, origin, offset, world.Globals.Rules.FlatWorld);
            if (!map.Geometry.Canonical(point) || map.TileAt(point).Ocean != map.TileAt(origin).Ocean
                || map.UnitOwner(point) is null)
            {
                continue;
            }

            changed |= Remember(world, observer, point);
            if (map.CityOwner(point) is not null)
            {
                foreach (ref var city in world.Entities.Cities.Scanned)
                {
                    if (city.Token.Occupied && city.Position == point)
                    {
                        changed |= !city.KnownTo.Contains(observer) || city.RememberedSize[observer] != city.Size;
                        city.KnownTo = city.KnownTo.With(observer);
                        city.RememberedSize[observer] = unchecked((byte)city.Size);
                        break;
                    }
                }

                continue;
            }

            foreach (ref var unit in world.Entities.Units.Scanned)
            {
                if (unit.Token.Occupied && unit.Position == point && unit.Owner.Value != observer)
                {
                    changed |= !unit.Visibility.Contains(observer);
                    unit.Visibility = unit.Visibility.With(observer);
                }
            }
        }

        return changed;
    }
}
