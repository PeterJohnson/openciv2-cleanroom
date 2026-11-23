using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A08 §3 post-city rates/strategic score adjustments and government choice.
/// F04 owns the preceding city score accumulation; V02 executes the chosen transition.</summary>
public sealed class GovernmentPolicy(GameState world, RuleState rules, GovernmentChanges changes, GameplayRandom random)
{
    private bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
    private bool Liberty(int owner) => BuildingQueries.HasWonder(world, rules.Definitions, owner, WonderIds.StatueOfLiberty);
    private Leader Leader(int owner) => rules.Leaders[world.Civilizations[owner].Tribe];
    private ref Civilization BestHuman => ref world.Civilizations[world.Globals.BestHumanRank];

    public void AfterCities(int owner, bool largeBalancedCity, bool disorder)
    {
        MapKnowledge.RequireLocal(world);
        if (!world.Globals.HumanPlayers.Contains(owner))
        {
            Rates(owner, largeBalancedCity, disorder);
        }

        StrategicScores(owner);
    }

    private void Rates(int owner, bool balanced, bool disorder)
    {
        unchecked
        {
            ref var civ = ref world.Civilizations[owner];
            int luxury = 10 - (civ.ScienceRate + civ.TaxRate);
            if (disorder && luxury < 4)
            {
                ++luxury;
            }

            if (world.Globals.Turn % 4 == 0 && !balanced && !disorder && luxury > 2)
            {
                --luxury;
            }

            luxury = Arithmetic.ClampAB(luxury, 0, 4);
            civ.ScienceRate = (byte)(Leader(owner).Civilize + ((10 - luxury) >> 1));
            if (civ.Gold > world.Globals.Turn + 100)
            {
                ++civ.ScienceRate;
            }

            if (civ.Gold > 2000)
            {
                ++civ.ScienceRate;
            }

            if (civ.Gold > 8000)
            {
                civ.ScienceRate = (byte)(10 - luxury);
            }

            bool expansion = false;
            for (int region = 1; region < MapLimits.Regions; ++region)
            {
                if (civ.ContinentCities[region] != 0 && (civ.ContinentPolicy[region] is 0 or 1
                    || civ.ContinentPolicy[region] == 5 && civ.ContinentFlags[region] != default))
                {
                    expansion = true;
                }
            }

            if (!expansion && civ.ScienceRate < 7)
            {
                ++civ.ScienceRate;
            }

            int cap = 6, government = (byte)civ.Government;
            if (government > (int)Government.Despotism)
            {
                cap = 7;
            }

            if (government > (int)Government.Fundamentalism)
            {
                cap = 8;
            }

            if (government > (int)Government.Republic)
            {
                cap = 10;
            }

            civ.ScienceRate = (byte)Arithmetic.ClampAB(civ.ScienceRate, 0, Math.Min(cap, 10 - luxury));
            if (Knows(owner, AdvanceIds.FusionPower) && Knows(owner, AdvanceIds.Laser)
                && Knows(owner, (int)rules.Definitions.Improvements[BuildingIds.Structural].Prerequisite)
                && Knows(owner, (int)rules.Definitions.Improvements[BuildingIds.Component].Prerequisite)
                && Knows(owner, (int)rules.Definitions.Improvements[BuildingIds.Module].Prerequisite))
            {
                civ.ScienceRate = 0;
            }

            civ.TaxRate = (byte)(10 - (civ.ScienceRate + luxury));
        }
    }

    private void StrategicScores(int owner)
    {
        unchecked
        {
            ref var civ = ref world.Civilizations[owner];
            int democracy = (int)Government.Democracy, republic = (int)Government.Republic;
            int bonus = 0;
            if ((Knows(owner, AdvanceIds.Democracy) || Liberty(owner)) && BestHuman.Government == Government.Fundamentalism)
            {
                bonus = BestHuman.SubversionCount + 1;
            }

            civ.GovernmentScore[democracy] = (short)(civ.GovernmentScore[democracy]
                + (BestHuman.UnitsActive[UnitTypeIds.Diplomat] + BestHuman.UnitsActive[UnitTypeIds.Spy] + BestHuman.SubversionCount)
                    * (BestHuman.SubversionCount + bonus));
            if (civ.Gold < BestHuman.Gold)
            {
                civ.GovernmentScore[democracy] += 10;
            }

            if (bonus == 0 && (Knows(owner, AdvanceIds.Communism) || Knows(owner, AdvanceIds.Fundamentalism) || Liberty(owner)))
            {
                int attack = Leader(owner).Attack;
                bool race = SpaceshipQueries.RaceActive(world);
                if (world.Globals.PowerRank[owner] <= attack + (race ? 4 : 3) && civ.NumAdvances < BestHuman.NumAdvances)
                {
                    civ.GovernmentScore[democracy] = civ.GovernmentScore[republic] = 0;
                }
            }
            if (Leader(owner).Attack >= 0)
            {
                if (!world.Globals.HumanPlayers.Contains(world.Globals.CivByRank[7])
                    && (Leader(owner).Attack == 1 || Leader(owner).Attack == 0 && Leader(owner).Civilize < 0))
                {
                    civ.GovernmentScore[democracy] = (short)(3 * civ.GovernmentScore[democracy] / 4);
                    civ.GovernmentScore[republic] = (short)(3 * civ.GovernmentScore[republic] / 4);
                }
                if (Leader(owner).Attack != 0 || Leader(owner).Civilize < 0 && bonus == 0)
                {
                    int a = 2 * Leader(owner).Attack - Leader(owner).Civilize;
                    if (world.Globals.PowerRank[world.Globals.BestHumanRank] - 1 <= world.Globals.PowerRank[owner]
                        || BestHuman.NumAdvances <= civ.NumAdvances)
                    {
                        if (BestHuman.NumAdvances <= civ.NumAdvances + 4 * a)
                        {
                            civ.GovernmentScore[democracy] = civ.GovernmentScore[republic] = 0;
                        }
                        else if (BestHuman.NumAdvances <= civ.NumAdvances + 8 * a)
                        {
                            civ.GovernmentScore[democracy] /= 2;
                            civ.GovernmentScore[republic] /= 2;
                        }
                    }
                }
            }
            if (civ.SpaceshipFlags.Launched && world.Globals.Difficulty > Difficulty.King)
            {
                civ.GovernmentScore[democracy] /= 2;
                civ.GovernmentScore[republic] /= 2;
            }
            if (world.Globals.Rules.Bloodlust && civ.ScienceRate == 0)
            {
                civ.GovernmentScore[democracy] = 0;
                if (Leader(owner).Attack >= 0)
                {
                    civ.GovernmentScore[republic] = 0;
                }
            }
            for (int w = 0; w < RuleLimits.Wonders; ++w)
            {
                world.Production.WonderNotices[owner][w] = default;
            }
        }
    }

    public void Choose(int owner, bool restricted, string call)
    {
        MapKnowledge.RequireLocal(world);
        ref var civ = ref world.Civilizations[owner];
        if (world.Globals.Status.HasScenario && world.Scenario.Flags.GovernmentLock && (byte)civ.Government >= (int)Government.Despotism)
        {
            return;
        }

        int maximum = (int)Government.Democracy;
        if (restricted)
        {
            maximum = (int)Government.Republic;
            if (random.PrimaryModulo(new ChoiceKey("A08.government-limit", call), 3) != 0)
            {
                maximum = (int)Government.Fundamentalism;
            }
        }
        if (world.Globals.Status.AiCoordination)
        {
            int difference = world.Civilizations[world.Globals.PlayerCiv].NumAdvances - civ.NumAdvances;
            if (world.Globals.PowerRank[owner] < 6)
            {
                if (difference > 6)
                {
                    civ.GovernmentScore[(int)Government.Democracy] = -2;
                }

                if (difference > 8)
                {
                    civ.GovernmentScore[(int)Government.Republic] = -1;
                }
            }
        }
        if (civ.GovernmentScore[(int)Government.Anarchy] > 0)
        {
            maximum = (byte)civ.Government <= (int)Government.Republic ? (int)Government.Despotism : (int)Government.Communism;
        }

        int best = -999, selected = (int)Government.Despotism;
        for (int government = (int)Government.Despotism; government <= maximum; ++government)
        {
            if (changes.Available(owner, government) && civ.GovernmentScore[government] >= best)
            { best = civ.GovernmentScore[government]; selected = government; }
        }

        changes.Change(owner, selected);
    }
}
