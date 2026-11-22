using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Ai;

/// <summary>A08 §2 classic advance valuation over live engine state. This is also used for human advice;
/// no RNG is consumed and no technology, production or diplomacy state is changed.</summary>
public static class AdvanceValuation
{
    private static readonly (int Unit, int Bonus)[] ExpansionUnits =
    [
        (UnitTypeIds.Trireme, 3), (UnitTypeIds.Caravel, 2),
        (UnitTypeIds.Frigate, 2), (UnitTypeIds.Transport, 1)
    ];

    /// <summary>Evaluates one backed advance using current leader, knowledge, city, regional and wonder inputs.
    /// </summary>
    public static int Value(GameState world, RuleState installed, int civilization, int advance)
    {
        var rules = installed.Definitions;
        ref readonly var civ = ref world.Civilizations[civilization];
        var leader = installed.Leaders[civ.Tribe];
        bool human = world.Globals.HumanPlayers.Contains(civilization);
        int civilize = leader.Civilize;
        if (!human && civilize >= 0)
        {
            for (int other = 1; other < EntityLimits.Civilizations; ++other)
            {
                ref readonly var opponent = ref world.Civilizations[other];
                if (!world.Globals.HumanPlayers.Contains(other) || !civ.Treaties[other].War
                    || civ.NumAdvances >= opponent.NumAdvances)
                {
                    continue;
                }

                for (int region = 1; region < MapLimits.Regions - 1; ++region)
                {
                    if (civ.ContinentCities[region] != 0 && opponent.ContinentCities[region] != 0)
                    {
                        if (civilize >= 0)
                        {
                            --civilize;
                        }

                        if (opponent.Reputation > 6)
                        {
                            civilize = -1;
                        }
                    }
                }
            }
        }

        int score = rules.Advances[advance].CivilizedAiModifier * civilize + rules.Advances[advance].AiValue;
        int expansion = Expansion(rules, advance);
        if (world.Globals.Status.LargeMap)
        {
            expansion *= 2;
        }

        bool freeRegion = false, humanContact = false;
        if (expansion != 0)
        {
            for (int region = 1; region < MapLimits.Regions - 1; ++region)
            {
                if (civ.ContinentCities[region] == 0)
                {
                    continue;
                }

                bool contested = false;
                for (int other = 0; other < EntityLimits.Civilizations; ++other)
                {
                    // A08 explicitly tests the evaluating civilization's own presence here.
                    if (other != civilization
                        && (civ.ContinentCities[region] != 0 || civ.ContinentMilitary[region] != 0))
                    {
                        contested = true;
                        humanContact |= human;
                        break;
                    }
                }

                if (!contested)
                {
                    freeRegion = true;
                    break;
                }
            }

            score += freeRegion ? expansion : !humanContact || world.Globals.Status.LargeMap ? 1 : 0;
        }

        if ((int)rules.Improvements[BuildingIds.PortFacility].Prerequisite == advance)
        {
            score += civ.ShipCount / 4;
        }

        if (!human)
        {
            score += AiBonuses(world, rules, civilization, advance);
        }

        if ((int)rules.Improvements[BuildingIds.Aqueduct].Prerequisite == advance
            || (int)rules.Improvements[BuildingIds.SewerSystem].Prerequisite == advance)
        {
            int maximum = 0;
            foreach (ref readonly var city in world.Entities.Cities.Scanned)
            {
                if (city.Token.Occupied && city.Owner.Value == civilization)
                {
                    maximum = Math.Max(maximum, city.Size);
                }
            }

            int limit = (int)rules.Improvements[BuildingIds.Aqueduct].Prerequisite == advance
                ? rules.Cosmic.AqueductSize : rules.Cosmic.SewerSize;
            score += maximum >= limit ? 2 : 0;
        }

        if (leader.Civilize >= 0 && Knows(world, civilization, AdvanceIds.University))
        {
            if (Research.DescendsFrom(rules, AdvanceIds.Computers, advance))
            {
                score += leader.Civilize + 1;
            }

            if (advance == AdvanceIds.Computers)
            {
                score += 2;
            }
        }

        if (Knows(world, civilization, AdvanceIds.Invention)
            && Research.DescendsFrom(rules, AdvanceIds.Industrialization, advance))
        {
            score += Knows(world, civilization, AdvanceIds.Navigation) ? 2 : 1;
        }

        score += TribeBonus(civ.Tribe, advance, expansion, freeRegion);
        if (world.Globals.AdvanceKnownBy[advance].Count == 0)
        {
            ++score;
        }

        for (int child = 0; child < RuleLimits.Advances; ++child)
        {
            var prerequisites = rules.Advances[child].Prerequisites;
            if (((int)prerequisites[0] == advance || (int)prerequisites[1] == advance)
                && Knows(world, civilization, child))
            {
                --score;
            }
        }

        return score < 2 ? 1 : score;
    }

    private static bool Knows(GameState world, int civilization, int advance) =>
        CivilizationKnowledge.KnowsAdvance(world.Civilizations, civilization, advance);

    private static int Expansion(RuleTables rules, int advance)
    {
        foreach (var (unit, bonus) in ExpansionUnits)
        {
            if (Research.DescendsFrom(rules, (int)rules.Units[unit].Prerequisite, advance))
            {
                return bonus;
            }
        }

        return 0;
    }

    private static int AiBonuses(GameState world, RuleTables rules, int civilization, int advance)
    {
        int result = 0;
        var humans = world.Globals.HumanPlayers;
        if (world.Globals.AdvanceKnownBy[AdvanceIds.SpaceFlight].Intersects(humans))
        {
            result += Research.DescendsFrom(rules, AdvanceIds.FusionPower, advance) ? 2 : 0;
            for (int building = BuildingIds.Structural; building <= BuildingIds.Module; ++building)
            {
                result += (int)rules.Improvements[building].Prerequisite == advance ? 3 : 0;
            }
        }

        int bomber = (int)rules.Units[UnitTypeIds.Bomber].Prerequisite;
        if (bomber >= 0 && world.Globals.AdvanceKnownBy[bomber].Intersects(humans)
            && Research.DescendsFrom(rules, bomber, advance))
        {
            result += bomber == advance ? 5 : 2;
        }

        for (int wonder = 0; wonder < RuleLimits.Wonders; ++wonder)
        {
            if ((int)rules.WonderObsolescence[wonder] != advance)
            {
                continue;
            }

            if (BuildingQueries.HasWonder(world, rules, civilization, wonder)
                && advance != AdvanceIds.Industrialization)
            {
                result -= 2;
            }

            if (BuildingQueries.ActiveOwner(world, rules, wonder) is { } owner && humans.Contains(owner))
            {
                result += 2;
            }
        }

        return result;
    }

    private static int TribeBonus(int tribe, int advance, int expansion, bool freeRegion)
    {
        return tribe switch
        {
            0 => advance is AdvanceIds.IronWorking or AdvanceIds.BronzeWorking or AdvanceIds.WarriorCode ? 2
                : advance == AdvanceIds.Monotheism ? -1 : 0,
            1 => advance == AdvanceIds.CodeOfLaws ? 1 : 0,
            2 => advance is AdvanceIds.Banking or AdvanceIds.Theology or AdvanceIds.Philosophy ? 1 : 0,
            3 => advance == AdvanceIds.Masonry ? 2 : 0,
            4 => advance == AdvanceIds.Democracy ? 2 : advance == AdvanceIds.Communism ? -1
                : advance is AdvanceIds.Rocketry or AdvanceIds.Computers or AdvanceIds.Leadership ? 1 : 0,
            5 => advance is AdvanceIds.Polytheism or AdvanceIds.BronzeWorking
                or AdvanceIds.Alphabet or AdvanceIds.MapMaking ? 1
                : advance == AdvanceIds.Monotheism ? -1 : advance == AdvanceIds.Philosophy ? 2 : 0,
            6 or 18 or 19 => PolytheistBonus(advance),
            7 => advance == AdvanceIds.Communism ? 2
                : advance is AdvanceIds.Philosophy or AdvanceIds.GuerrillaWarfare ? 1 : 0,
            8 => advance is AdvanceIds.BronzeWorking or AdvanceIds.IronWorking ? -1
                : advance == AdvanceIds.Monotheism ? 0 : PolytheistBonus(advance),
            9 => advance is AdvanceIds.Leadership or AdvanceIds.Tactics or AdvanceIds.Conscription ? 1 : 0,
            10 => advance == AdvanceIds.Gunpowder ? -2 : advance == AdvanceIds.Monotheism ? -1 : 0,
            11 => (expansion != 0 && freeRegion ? -expansion : 0) + (advance == AdvanceIds.Gunpowder ? 1 : 0),
            12 => (advance == AdvanceIds.Monotheism ? 1 : 0) + (expansion != 0 ? 1 : 0),
            15 => advance is AdvanceIds.Steel or AdvanceIds.Miniaturization ? 1 : 0,
            16 => advance == AdvanceIds.MapMaking ? 1 : 0,
            17 => (expansion > 1 ? 1 : 0) + (advance == AdvanceIds.Monotheism ? 1 : 0),
            20 => advance == AdvanceIds.HorsebackRiding ? 2 : 0,
            _ => 0
        };
    }

    private static int PolytheistBonus(int advance) => advance == AdvanceIds.Polytheism ? 2
        : advance is AdvanceIds.HorsebackRiding or AdvanceIds.Mysticism or AdvanceIds.CeremonialBurial ? 1
        : advance == AdvanceIds.Monotheism ? -1 : 0;
}
