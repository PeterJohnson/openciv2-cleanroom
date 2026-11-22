using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>V03 research in the authored single-player profile, with ordinary advances only.</summary>
public static class LocalResearch
{
    public static bool Available(GameState world, RuleTables rules, int owner, int advance) =>
        advance >= 0 && advance < rules.Advances.Length && advance != CivilizationKnowledge.FutureTechnology
        && Research.Available(world, rules, owner, advance);

    public static int Cost(GameState world, RuleTables rules, int owner)
    {
        int count = Sum(world.Civilizations[owner]);
        int n = count < 2 ? 1 : count;
        // V03 indexes the leader by the best-human rank itself.
        int leader = Sum(world.Civilizations[world.Globals.BestHumanRank]);
        int difficulty = (int)world.Globals.Difficulty;
        int multiplier = 2 * Math.Min(difficulty, 4) + 6;
        if (n < leader)
        {
            if (difficulty != 0)
            {
                --multiplier;
            }

            if (difficulty == (int)Difficulty.Deity && n + 4 < leader && world.Globals.Turn > 150)
            {
                --multiplier;
            }
        }
        else
        {
            multiplier += (n - leader) / 3;
        }

        if (n > 19)
        {
            multiplier += Math.Clamp(n - world.Globals.Turn / 8, 0, 6);
        }

        multiplier = rules.Cosmic.TechnologyCostParadigm * multiplier / 10;
        int extra = multiplier * 3 >> 2;
        multiplier += count < 20 ? count * extra / 20 : extra;
        if (rules.EnabledAdvanceCount > 67)
        {
            multiplier = multiplier * 67 / rules.EnabledAdvanceCount;
        }

        if (world.Globals.Status.LargeMap)
        {
            multiplier = multiplier * 5 / 4;
        }

        if (world.Globals.Status.SmallMap)
        {
            multiplier = multiplier * 4 / 5;
        }

        int cost = Math.Max(multiplier, 11 - n) * n;
        return cost is >= 1 and <= 32000 ? cost : 32000;
    }

    private static int Sum(in Civilization civ) => civ.NumAdvances + civ.ResearchCostAdvances;

    /// <summary>Accumulates positive city science and grants at most one ordinary advance.</summary>
    public static int? Accumulate(GameState world, RuleTables rules, int owner, int points)
    {
        if (points <= 0)
        {
            return null;
        }

        ref var civ = ref world.Civilizations[owner];
        civ.ResearchProgress = unchecked((short)(civ.ResearchProgress + points));
        int advance = civ.Researching;
        if (advance < 0 || civ.ResearchProgress < Cost(world, rules, owner))
        {
            return null;
        }

        civ.ResearchProgress = 0;
        civ.Researching = -1;
        civ.Advances = civ.Advances.With(advance);
        civ.AdvanceSource[advance] = 0;
        if (world.Globals.AdvanceKnownBy[advance].Count == 0)
        {
            world.Globals.FirstDiscoverer[advance] = (byte)owner;
        }

        world.Globals.AdvanceKnownBy[advance] = world.Globals.AdvanceKnownBy[advance].With(owner);
        ++civ.NumAdvances;
        return advance;
    }
}
