using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>U03 road/rail query: validate the chosen uniform route, retaining its cache and path selectors.</summary>
public static class RoadConnections
{
    public static int Query(GameState world, RuleTables rules, int civilization, MapPoint source, MapPoint target)
    {
        var map = world.Map;
        bool flat = world.Globals.Rules.FlatWorld;
        if (map.LandRegion(source) != map.LandRegion(target) || map.Geometry.StepDistance(source, target, flat) >= 23)
        {
            return 0;
        }

        var paths = world.Paths;
        paths.UniformCost = paths.RoadsOnly = true;
        paths.Owner = -1;
        paths.Goal = target;
        paths.SelectedUnitType = UnitTypeIds.Warriors;
        var current = source;
        bool blocked = true, allRail = true;
        int steps = 0;
        for (; ; )
        {
            int direction = GenerationConnectivity.UniformDirection(map, flat, rules, paths, current, 99);
            if (direction is < 0 or >= 8)
            {
                break;
            }

            current = MapNeighborhoods.Offset(map.Geometry, current, MapNeighborhoods.Directions[direction], flat);
            if (!map.Geometry.InBounds(current))
            {
                break;
            }

            if (current == target || map.CityOwner(current) == civilization)
            { blocked = false; break; }
            if (map.CityOwner(current) is { } cityOwner && !world.Civilizations[civilization].Treaties[cityOwner].Alliance)
            {
                break;
            }

            if (map.CityOrUnitOwner(current) is > 0 and var owner && owner != civilization
                && !world.Civilizations[civilization].Treaties[owner].Alliance)
            {
                break;
            }

            var features = map.TileAt(current).Features;
            if (!features.HasFlag(MapFeatures.Road))
            {
                break;
            }

            if (!features.HasFlag(MapFeatures.Railroad))
            {
                allRail = false;
            }

            if (++steps > 50)
            {
                break;
            }
        }
        paths.UniformCost = paths.RoadsOnly = false;
        return blocked ? 0 : allRail ? 2 : 1;
    }
}
