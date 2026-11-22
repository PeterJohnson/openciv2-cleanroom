using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>W04 local irrigation source, including the center after the four Q diagonals.</summary>
public static class TerrainSources
{
    public static bool Irrigation(GameState world, MapPoint point)
    {
        foreach (var offset in MapNeighborhoods.Diagonal)
        {
            var p = MapNeighborhoods.Offset(world.Map.Geometry, point, offset, world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(p))
            {
                continue;
            }

            ref var tile = ref world.Map.TileAt(p);
            if (tile.Ocean || tile.River || tile.Features.HasFlag(MapFeatures.Irrigation))
            {
                return true;
            }
        }
        return false;
    }
}
