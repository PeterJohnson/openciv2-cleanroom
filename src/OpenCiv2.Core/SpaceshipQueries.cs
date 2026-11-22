using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>V07 §§2,8 pure capacity, race and arrival predicates. These do not assemble, launch,
/// calculate performance, mutate preview years or process arrival outcomes.</summary>
public static class SpaceshipQueries
{
    private static readonly int[] Maxima = [39, 8, 8, 4, 4, 4];

    public static int PartLimit(GameState world, int owner, int part)
    {
        int maximum = Maxima[part];
        if (part == 0)
        {
            return maximum;
        }

        int structural = world.Civilizations[owner].SpaceshipParts[0];
        if (part <= 2)
        {
            return Arithmetic.ClampAB(maximum, 0, (structural + 1 - (2 * part - 2)) / 4);
        }

        int limit = Arithmetic.ClampAB(maximum, 0, (structural + 1 - (4 * part - 12)) / 8);
        if (!world.Globals.HumanPlayers.Contains(owner))
        {
            limit = Arithmetic.ClampAB(limit, 0, 1);
        }

        return Math.Max(0, limit);
    }

    public static int PartCount(GameState world, int owner, int part) =>
        Arithmetic.ClampAB(world.Civilizations[owner].SpaceshipParts[part], 0, PartLimit(world, owner, part));

    public static int GroupMaximum(int group) => group switch { 0 => 39, 1 => 16, _ => 12 };
    public static int GroupCount(GameState world, int owner, int group) => Sum(group, p => world.Civilizations[owner].SpaceshipParts[p]);
    public static int GroupLimit(GameState world, int owner, int group) => Sum(group, p => PartLimit(world, owner, p));
    public static int GroupCountClamped(GameState world, int owner, int group) => Sum(group, p => PartCount(world, owner, p));
    public static bool GroupFull(GameState world, int owner, int group) => GroupMaximum(group) <= GroupCount(world, owner, group);
    public static bool GroupAtLimit(GameState world, int owner, int group) => GroupLimit(world, owner, group) <= GroupCount(world, owner, group);

    public static bool GroupCanBuild(GameState world, RuleTables rules, int owner, int group) =>
        (!GroupAtLimit(world, owner, group) || GroupCount(world, owner, group) == 0)
        && !GroupFull(world, owner, group)
        && CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, (int)rules.Improvements[unchecked(group + 35)].Prerequisite);

    public static bool PartMaxed(GameState world, int owner, int improvement)
    {
        ref var parts = ref world.Civilizations[owner].SpaceshipParts;
        return improvement switch
        {
            BuildingIds.Structural => parts[0] >= 39,
            BuildingIds.Component => parts[1] >= 8 && parts[2] >= 8,
            _ => parts[3] >= 4 && parts[4] >= 4 && parts[5] >= 4
        };
    }

    private static int Sum(int group, Func<int, int> count) => group switch
    {
        0 => count(0),
        1 => unchecked(count(2) + count(1)),
        _ => unchecked(count(3) + count(4) + count(5))
    };

    /// <summary>Equivalent closed form of the per-engine increment loop, retaining signed32 wrap.</summary>
    public static int Thrust(int engines) => engines <= 0 ? 0 : engines <= 4 ? engines
        : engines <= 6 ? unchecked(4 + 2 * (engines - 4)) : unchecked(8 + 3 * (engines - 6));

    public static bool RaceActive(GameState world) => !world.Globals.Rules.Bloodlust
        && (world.Globals.WonderCity[WonderIds.Apollo] != BuildingQueries.Unbuilt
            || world.Globals.HumanPlayers.Intersects(world.Globals.AdvanceKnownBy[AdvanceIds.SpaceFlight]));

    public static bool RaceBehind(GameState world, RuleTables rules, int owner)
    {
        if (!RaceActive(world) || world.Civilizations[owner].SpaceshipFlags.Launched || world.Globals.Status.Won
            || world.Globals.Difficulty == Difficulty.Chieftain || world.Globals.HumanPlayers.Contains(owner))
        {
            return false;
        }

        for (int human = 1; human < EntityLimits.Civilizations; ++human)
        {
            if (world.Globals.HumanPlayers.Contains(human) && world.Civilizations[human].SpaceshipFlags.Launched)
            {
                return true;
            }
        }

        int own = Progress(world, rules, owner);
        for (int human = 1; human < EntityLimits.Civilizations; ++human)
        {
            if (world.Globals.HumanPlayers.Contains(human)
                && CivilizationKnowledge.KnowsAdvance(world.Civilizations, human, AdvanceIds.SpaceFlight)
                && human != owner && own <= Progress(world, rules, human))
            {
                return true;
            }
        }

        return false;
    }

    private static int Progress(GameState world, RuleTables rules, int owner)
    {
        int progress = 0;
        for (int part = 0; part < 6; ++part)
        {
            int group = part == 0 ? 0 : part <= 2 ? 1 : 2;
            progress = unchecked(progress + world.Civilizations[owner].SpaceshipParts[part] * rules.Improvements[group + 35].CostRows);
        }
        return progress;
    }

    public static bool Arrived(GameState world, int owner) => world.Civilizations[owner].SpaceshipFlags.Launched
        && world.Globals.Year >= world.Civilizations[owner].SpaceshipArrivalYear;
}
