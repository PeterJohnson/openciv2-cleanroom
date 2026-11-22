using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Core.Ai;

public sealed partial class CityProductionChoice
{
    private sealed partial class Evaluation
    {
        private void Wonders()
        {
            unchecked
            {
                for (int w = 0; w < rivalry.Length; ++w)
                {
                    int item = 39 + w;
                    if (!Build(item))
                    {
                        continue;
                    }

                    int d = P == 4 ? T : 0, group = w / 7;
                    int b = Wo + Wh + 8 - Civ.Gold / 200;
                    if (b < 2)
                    {
                        b = 1;
                    }

                    b += Civ.WondersByGroup[group];
                    if (Civ.WondersByGroup[group] > World.HumanWonders.Minimum[group])
                    {
                        b += 2;
                    }

                    if (Civ.WondersByGroup[group] < World.Civilizations[World.Globals.WorstHumanRank].WondersByGroup[group])
                    {
                        --b;
                    }

                    if (Wip && City.Production != -item)
                    {
                        int current = -City.Production - 39;
                        if (current < rivalry.Length && rivalry[current].Contains(0))
                        {
                            continue;
                        }

                        if (current < rivalry.Length && rivalry[current].Intersects(World.Globals.HumanPlayers) && World.Globals.WonderCity[current] == BuildingQueries.Unbuilt
                            && City.ShieldBox >= Rules.Improvements[-City.Production].CostRows * Work.ShieldRows / 2)
                        {
                            continue;
                        }
                    }
                    int prerequisite = (int)Rules.Improvements[item].Prerequisite;
                    if (Difficulty <= 1 && prerequisite >= 0 && !World.Globals.AdvanceKnownBy[prerequisite].Intersects(World.Globals.HumanPlayers))
                    {
                        continue;
                    }

                    if (w != WonderIds.Manhattan)
                    {
                        if (Difficulty < 2)
                        {
                            for (cursor = 0; cursor < World.Entities.Cities.End; ++cursor)
                            {
                                ref var other = ref World.Entities.Cities.Storage[cursor];
                                if (other.Token.Occupied && World.Globals.HumanPlayers.Contains(other.Owner.Value) && other.Production == -item)
                                {
                                    b = 999;
                                }
                            }
                            if (b == 999)
                            {
                                continue;
                            }
                        }
                        if (Knows(AdvanceIds.Rocketry) && !World.Globals.AdvanceKnownBy[AdvanceIds.Rocketry].Intersects(World.Globals.HumanPlayers))
                        {
                            continue;
                        }
                    }
                    if (!WonderAdjustment(w, ref b, ref d))
                    {
                        continue;
                    }

                    bool expired = BuildingQueries.Expired(World, Rules, w);
                    if (expired)
                    {
                        b = 3 * b + 50 * Difficulty;
                    }

                    b += 5 * Wb;
                    if ((Wb == 0 || Wip) && Civ.WondersByGroup[group] == 0)
                    {
                        b -= 2;
                    }

                    b += P == 1 ? E + 1 : E / 2;
                    if (City.Production == -item)
                    {
                        if (ForeignSame != 0)
                        {
                            b /= 2;
                        }

                        b = (Ahead + 1) * b + Ahead;
                    }
                    else if (City.Production is <= -39 and >= -66 && rivalry[-City.Production - 39].Contains(C))
                    {
                        b = 2 * b + 1;
                    }
                    // X01: all non-wonder rivalry contributions are zero.
                    int raw = b, cost = Rules.Improvements[item].CostRows * Work.ShieldRows + 1;
                    if (Wip && Urgent == 0 && !expired)
                    {
                        if (Wb == 0)
                        {
                            if (T == d)
                            {
                                b /= 2;
                            }
                            else
                            {
                                d = T;
                            }
                        }
                        if (City.ShieldBox >= 3 * cost / 4)
                        {
                            d = T;
                        }

                        if (T != d && City.ShieldBox >= cost / 2 && Wb < 3)
                        {
                            d = T;
                        }
                    }
                    if (!Wip)
                    {
                        if ((Civ.BestShieldSurplus >> 1) >= S)
                        {
                            b *= 2;
                        }
                        else if (Human && Civ.BestShieldSurplus != S && S < 6)
                        {
                            b *= 2;
                        }
                    }
                    if (Civ.BestShieldSurplus == S)
                    {
                        b -= b / 3;
                    }

                    b = Weight(b, d, false);
                    if (Has(BuildingIds.Barracks))
                    {
                        b += Div(b, (B >> 1) + 3);
                    }

                    if (Has(BuildingIds.Palace))
                    {
                        b -= b / 3;
                        if (Wh == 0 || Civ.WondersByGroup[group] == 0)
                        {
                            b /= 2;
                        }
                    }
                    if (City.Flags.WonderBuilt)
                    {
                        b *= 3;
                    }
                    else if (Wip && !expired)
                    {
                        b = (Div((cost - City.ShieldBox) * (3 * b / 4), cost) + b / 4) / 3;
                    }

                    if (IsWonder(choice))
                    {
                        if (raw >= bestRaw)
                        {
                            continue;
                        }

                        bestRaw = raw;
                        choice = bestBuild = -item;
                        choiceScore = Math.Min(choiceScore, b);
                        bestBuildScore = Math.Min(bestBuildScore, b);
                    }
                    else
                    {
                        if (b < choiceScore)
                        { choice = -item; choiceScore = b; bestRaw = raw; }
                        if (b < bestBuildScore)
                        { bestBuild = -item; bestBuildScore = b; }
                    }
                }
            }
        }

        private bool WonderAdjustment(int w, ref int b, ref int d)
        {
            unchecked
            {
                int cities = Civ.CityCount;
                switch (w)
                {
                    case WonderIds.Pyramids:
                        b -= 3 * Z + 2;
                        break;
                    case WonderIds.HangingGardens:
                        b -= cities / 3 + 1;
                        break;
                    case WonderIds.Colossus:
                        if (Has(BuildingIds.Palace))
                        {
                            --b;
                        }

                        if (Has(BuildingIds.Marketplace))
                        {
                            --b;
                        }

                        b -= City.Size / 3;
                        break;
                    case WonderIds.Lighthouse:
                        b -= (Active(UnitTypeIds.Caravel) + 2 + Active(UnitTypeIds.Trireme)) >> 2;
                        break;
                    case WonderIds.GreatLibrary:
                        for (int c = 1; c < EntityLimits.Civilizations; ++c)
                        {
                            if (World.Civilizations[c].NumAdvances < Civ.NumAdvances)
                            {
                                if (World.Globals.HumanPlayers.Contains(c))
                                {
                                    ++b;
                                }
                            }
                            else
                            {
                                b -= World.Globals.HumanPlayers.Contains(c) && World.Civilizations[c].NumAdvances > Civ.NumAdvances + 5 ? 3 : 1;
                            }
                        }

                        break;
                    case WonderIds.Oracle:
                        b -= cities / 4 - World.Globals.Turn / 25;
                        break;
                    case WonderIds.GreatWall:
                        b -= 2 * Z;
                        break;
                    case WonderIds.SunTzu:
                        b -= 3 * A + X - 2 * Z + 1;
                        break;
                    case WonderIds.KingRichard:
                        b -= Clamp(2 * Work.Shields / 3, 0, 3);
                        break;
                    case WonderIds.MarcoPolo:
                        for (int c = 1; c < EntityLimits.Civilizations; ++c)
                        {
                            if (World.Globals.HumanPlayers.Contains(c))
                            {
                                if (Civ.Treaties[c].Alliance)
                                {
                                    --b;
                                }

                                if (Civ.Treaties[c].Ceasefire || Civ.Treaties[c].Peace)
                                {
                                    --b;
                                }
                            }
                        }

                        break;
                    case WonderIds.Michelangelo:
                    case WonderIds.AdamSmith:
                        b -= cities / 4;
                        break;
                    case WonderIds.Copernicus:
                        b -= Q / 8 + 1;
                        if (Has(BuildingIds.Palace))
                        {
                            --b;
                        }

                        break;
                    case WonderIds.Magellan:
                        int ships = 0;
                        for (int t = 0; t < RuleLimits.UnitTypes; ++t)
                        {
                            if (Rules.Units[t].Domain == RuleDomain.Sea)
                            {
                                ships += Active(t);
                            }
                        }

                        b -= (ships + 3) / 4;
                        break;
                    case WonderIds.Shakespeare:
                        b -= City.Size / 5;
                        break;
                    case WonderIds.Leonardo:
                        d = T;
                        b -= 4;
                        break;
                    case WonderIds.Bach:
                        b -= Civ.ContinentCitySize[R] >> 4;
                        if (Has(BuildingIds.Palace))
                        {
                            --b;
                        }

                        break;
                    case WonderIds.Newton:
                        b -= City.Size / 6;
                        if (Has(BuildingIds.Palace))
                        {
                            --b;
                        }

                        break;
                    case WonderIds.Darwin:
                        b -= 3;
                        break;
                    case WonderIds.StatueOfLiberty:
                        b -= Knows(AdvanceIds.Communism) ? 1 : 2;
                        break;
                    case WonderIds.EiffelTower:
                        int strongest = World.Globals.CivByRank[7];
                        if (World.Globals.HumanPlayers.Contains(strongest))
                        {
                            b -= World.Civilizations[strongest].Reputation > 3 ? 2 : 1;
                            if (World.Civilizations[strongest].CityCount > 7)
                            {
                                b -= World.Globals.PowerRank[C] == 6 ? 2 : 1;
                            }
                        }
                        break;
                    case WonderIds.WomensSuffrage:
                        b -= A >= 0 && X >= 0 && Z >= 0 ? 2 : 1;
                        if (Civ.GovernmentScore[(int)Contracts.Government.Republic] > 0)
                        {
                            --b;
                        }

                        if (Government > (int)Contracts.Government.Fundamentalism)
                        {
                            --b;
                        }

                        if (Government > (int)Contracts.Government.Republic)
                        {
                            --b;
                        }

                        break;
                    case WonderIds.HooverDam:
                        b -= Civ.ContinentCitySize[R] / 20;
                        break;
                    case WonderIds.Manhattan:
                        if (!Knows(AdvanceIds.Rocketry))
                        {
                            return false;
                        }

                        for (int c = 1; c < EntityLimits.Civilizations; ++c)
                        {
                            if (World.Globals.HumanPlayers.Contains(c) && Knows(c, AdvanceIds.Rocketry) && World.Civilizations[c].MilitaryStrength < Civ.MilitaryStrength)
                            {
                                return false;
                            }
                        }

                        int leader = World.Globals.CivByRank[7];
                        if (World.Globals.HumanPlayers.Contains(leader))
                        {
                            --b;
                            if (!Knows(leader, AdvanceIds.Rocketry))
                            {
                                --b;
                            }
                        }
                        b += 2 * Z - A;
                        break;
                    case WonderIds.UnitedNations:
                        b -= Z - 2 * A;
                        if (World.Globals.PowerRank[C] == 7)
                        {
                            --b;
                        }

                        break;
                    case WonderIds.Apollo:
                        b -= 3 * Z - 2 * A;
                        break;
                    case WonderIds.Seti:
                        b -= 2 * Z + 2;
                        break;
                    case WonderIds.CureForCancer:
                        b -= A + 2;
                        break;
                }
                return true;
            }
        }
    }
}
