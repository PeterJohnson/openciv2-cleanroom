using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Ai;

public sealed partial class CivilizationAssessment
{
    private sealed partial class Assessment
    {
        private void Regions()
        {
            Civ.ContinentPolicy[0] = Civ.ContinentPolicy[63] = 5;
            Civ.Flags.AtWar = false;
            for (int other = 1; other < EntityLimits.Civilizations; ++other)
            {
                if (Civ.Treaties[other].War)
                {
                    Civ.Flags.AtWar = true;
                }
            }

            for (int region = 1; region < 63; ++region)
            {
                int attack = 0, defend = 0, threat = 0, combinedMilitary = 0, combinedCities = 0;
                int oldPolicy = Civ.ContinentPolicy[region];
                for (int other = 1; other < EntityLimits.Civilizations; ++other)
                {
                    ref var rival = ref World.Civilizations[other];
                    if (rival.ContinentMilitary[region] == 0 || !(Civ.ContinentAttack[region] / 4 < rival.ContinentAttack[region] || rival.ContinentCities[region] != 0))
                    {
                        continue;
                    }

                    if (other != owner)
                    {
                        if (DiplomaticAttitudes.Hostile(World, owner, other) || Civ.Treaties[other].HostileIntent)
                        {
                            if (rival.ContinentMilitary[region] < Civ.ContinentAttack[region]
                                || rival.ContinentAttack[region] < Civ.ContinentMilitary[region] || Civ.ContinentCities[region] == 0)
                            {
                                ++attack;
                            }
                            else
                            {
                                ++defend;
                            }
                        }
                        if (rival.ContinentMilitary[region] != 0 && World.Globals.HumanPlayers.Contains(other)
                            && (Civ.Treaties[other].Ceasefire || Civ.Treaties[other].Peace)
                            && Civ.ContinentAttack[region] / 2 + Civ.ContinentMilitary[region] < rival.ContinentAttack[region])
                        {
                            ++threat;
                        }
                    }
                    combinedMilitary += rival.ContinentMilitary[region];
                    combinedCities += rival.ContinentCities[region];
                    if (other != owner)
                    {
                        if (Civ.Treaties[other].Alliance)
                        { combinedMilitary += rival.ContinentMilitary[region]; combinedCities += rival.ContinentCities[region]; }
                        else
                        {
                            ref var flags = ref Civ.ContinentFlags[region];
                            if (rival.ContinentCities[region] != 0)
                            {
                                if (Civ.Treaties[other].Peace)
                                {
                                    flags.CitiesWithPeace = true;
                                }
                                else
                                {
                                    flags.CitiesWithoutPeace = true;
                                }
                            }
                            if (rival.ContinentMilitary[region] != 0)
                            {
                                if (Civ.Treaties[other].Peace)
                                {
                                    flags.MilitaryWithPeace = true;
                                }
                                else
                                {
                                    flags.MilitaryWithoutPeace = true;
                                }
                            }
                        }
                    }
                }
                if (World.Civilizations[0].ContinentCities[region] != 0)
                {
                    ++attack;
                }

                int density = World.Globals.HumanPlayers.Contains(World.Globals.CivByRank[7])
                    && World.Civilizations[World.Globals.CivByRank[7]].CityCount > 4 && World.Globals.Turn > 150 ? 7 : 6;
                if (World.Globals.Status.LargeMap)
                {
                    ++density;
                }

                bool crowded = 2 * (Civ.ContinentMilitary[region] + combinedMilitary) > World.Map.Regions[region].Land;
                if (Civ.ContinentFlags[region] == default)
                {
                    density += Expansion + 1;
                }

                if ((Civ.ContinentCities[region] + combinedCities) * density + 2 > World.Map.Regions[region].Fertile)
                {
                    crowded = true;
                }

                if (!Knows(AdvanceIds.MapMaking))
                {
                    crowded = false;
                }

                Civ.ContinentPolicy[region] = (byte)(crowded ? 4 : 5);
                if (attack != 0)
                {
                    Civ.ContinentPolicy[region] = 0;
                }

                if (defend != 0 || threat != 0)
                {
                    Civ.ContinentPolicy[region] = 1;
                }

                if (defend != 0)
                {
                    Civ.ContinentFlags[region].StrongerEnemy = true;
                }

                if (Civ.ContinentMilitary[region] == 0 && Civ.ContinentCities[region] == 0
                    && !(Civ.ContinentFlags[region].MilitaryWithPeace && !Civ.ContinentFlags[region].MilitaryWithoutPeace))
                {
                    Civ.ContinentPolicy[region] = 0;
                }

                if (Civ.ContinentPolicy[region] != oldPolicy && !Human)
                {
                    parent.production.ResetCivilization(owner, region);
                }
            }
            if (Civ.ContinentCities[63] != 0 && landCities <= Civ.ContinentCities[63])
            {
                Civ.ContinentPolicy[63] = 4;
            }
        }
    }
}
