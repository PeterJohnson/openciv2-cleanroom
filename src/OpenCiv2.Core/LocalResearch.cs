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

    public static int Cost(GameState world, RuleTables rules, int owner) => Research.Cost(world, rules, owner);

    /// <summary>Accumulates positive city science and grants at most one ordinary advance.</summary>
    public static int? Accumulate(GameState world, RuleTables rules, ResearchGrants grants, int owner, int points, string call)
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
        grants.Grant(owner, advance, 0, false, call);
        return advance;
    }
}
