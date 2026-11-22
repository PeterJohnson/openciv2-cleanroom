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
        bool changed = MapKnowledge.Remember(world, observer, point);
        changed |= MapKnowledge.Explore(world, observer, point);
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
    public static bool RevealAdjacentOccupants(GameState world, int observer, MapPoint origin) =>
        MapKnowledge.RevealAdjacentUnits(world, new(world, new(100000, 64)), observer, origin);
}
