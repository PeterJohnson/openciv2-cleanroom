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

}
