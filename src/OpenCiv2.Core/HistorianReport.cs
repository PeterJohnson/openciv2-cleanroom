using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record HistorianRank(int Rank, int Civilization);

/// <summary>P07 §5 adopted ranking/disclosure. The stock topic is wealth; presentation
/// topics and wording may change. No simulation or content random sample is needed.</summary>
public sealed record HistorianReport(OwnedList<HistorianRank> Ranks)
{
    public static HistorianReport Prepare(GameState world)
    {
        MapKnowledge.RequireLocal(world);
        var totals = new int[EntityLimits.Civilizations];
        for (int c = 0; c < totals.Length; ++c)
        {
            totals[c] = world.Civilizations[c].Gold;
        }

        List<HistorianRank> ranks = [];
        for (int rank = 1; rank <= 7; ++rank)
        {
            int best = 0, selected = -1;
            for (int c = 1; c < totals.Length; ++c)
            {
                if (world.Globals.CivsAlive.Contains(c) && best <= totals[c])
                { best = totals[c]; selected = c; }
            }
            if (selected < 0)
            {
                break;
            }

            totals[selected] = -1;
            int local = world.Multiplayer.LocalHuman;
            if (selected == local || world.Civilizations[local].Treaties[selected].Contact || world.Globals.Difficulty == Difficulty.Chieftain)
            {
                ranks.Add(new(rank, selected));
            }
        }
        return new(new OwnedList<HistorianRank>(ranks));
    }
}
