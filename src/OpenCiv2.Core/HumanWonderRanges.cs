using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>F04 retained human wonder-group summaries, refreshed before global advancement.
/// C06 consumes the snapshot without recounting city wonders or filtering dead seats.</summary>
public sealed class HumanWonderRanges
{
    public Fixed4<int> Minimum, Maximum;

    public void Update(GameState world)
    {
        for (int group = 0; group < 4; ++group)
        {
            Minimum[group] = 99;
            Maximum[group] = 0;
            for (int owner = 1; owner < EntityLimits.Civilizations; ++owner)
            {
                if (!world.Globals.HumanPlayers.Contains(owner))
                {
                    continue;
                }

                int count = world.Civilizations[owner].WondersByGroup[group];
                Minimum[group] = Math.Min(Minimum[group], count);
                Maximum[group] = Math.Max(Maximum[group], count);
            }
        }
    }
}
