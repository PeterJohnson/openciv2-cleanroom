using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

public static class TerritoryQueries
{
    /// <summary>W01 peaceful foreign-radius query. Occupant ownership and city/unit presence do not
    /// substitute for a claimant; directed alliance suppresses directed peace.</summary>
    public static int? PeaceOwner(GameState world, MapPoint position, int civilization)
    {
        if (!world.Map.Geometry.InBounds(position) || civilization <= 0)
        {
            return null;
        }

        ref var tile = ref world.Map.TileAt(position);
        if (tile.Ocean || tile.Claimant == 0 || tile.Claimant == civilization)
        {
            return null;
        }

        var relation = world.Civilizations[civilization].Treaties[tile.Claimant];
        return !relation.Alliance && relation.Peace ? tile.Claimant : null;
    }
}
