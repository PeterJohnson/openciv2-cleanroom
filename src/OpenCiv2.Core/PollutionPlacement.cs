using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>W05 shared local pollution placement. Callers own eligibility, random draws and redraw.</summary>
public static class PollutionPlacement
{
    public static void Place(GameState world, MapPoint point)
    {
        MapKnowledge.RequireLocal(world);
        if (!world.Map.Geometry.InBounds(point) || world.Map.TileAt(point).Features.HasFlag(MapFeatures.Pollution))
        { return; }
        world.Map.Mutate(new TileChange.Features(point, MapFeatures.Pollution));
        for (int observer = 1; observer < EntityLimits.Civilizations; ++observer)
        { MapKnowledge.Remember(world, observer, point); }
        world.Globals.PollutedTiles = unchecked((short)(world.Globals.PollutedTiles + 1));
    }
}
