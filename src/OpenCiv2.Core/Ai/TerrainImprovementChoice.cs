using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Ai;

/// <summary>A03 §2 recommendation, including the irrigation-source gate on mining.</summary>
public static class TerrainImprovementChoice
{
    public static int Choose(GameState world, RuleTables rules, int owner, MapPoint point)
    {
        if (world.Map.CityOwner(point) is not null)
        {
            return 0;
        }

        var tile = world.Map.TileAt(point);
        if (tile.Features.HasFlag(MapFeatures.Mine) || tile.Features.HasFlag(MapFeatures.Irrigation) && !CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, AdvanceIds.Refrigeration))
        {
            return 0;
        }

        var terrain = tile.Terrain;
        if (!TerrainSources.Irrigation(world, point))
        {
            return 0;
        }

        var work = rules.Terrain[(int)terrain].Work ?? throw new InvalidOperationException("A03 requires terrain work rules");
        int threshold = unchecked((sbyte)work.Irrigation.AiGovernmentThreshold);
        if (work.Irrigation.Result != TerrainResult.NoResult && threshold != 0 && threshold <= (byte)world.Civilizations[owner].Government)
        {
            return 1;
        }

        if (tile.Features.HasFlag(MapFeatures.Irrigation))
        {
            return 0;
        }

        threshold = unchecked((sbyte)work.Mining.AiGovernmentThreshold);
        return work.Mining.Result != TerrainResult.NoResult && threshold != 0 && threshold <= (byte)world.Civilizations[owner].Government ? 2 : 0;
    }
}
