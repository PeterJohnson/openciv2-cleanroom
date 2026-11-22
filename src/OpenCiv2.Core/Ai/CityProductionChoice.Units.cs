using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Core.Ai;

public sealed partial class CityProductionChoice
{
    private sealed partial class Evaluation
    {
        private void SpecialUnits()
        {
            unchecked
            {
                for (int t = 0; t < RuleLimits.UnitTypes; ++t)
                {
                    int role = (sbyte)Rules.Units[t].Role;
                    if (role <= 5 || !Unit(t))
                    {
                        continue;
                    }

                    int b = 999, d = 0;
                    if (role == (int)RuleRole.Diplomacy)
                    {
                        if (!Diplomat(t, ref b, ref d))
                        {
                            continue;
                        }
                    }
                    else if (role == (int)RuleRole.Trade)
                    {
                        if (City.Flags.HadCaravan)
                        {
                            continue;
                        }

                        int routes = (sbyte)City.RouteCount;
                        b = routes < 3 ? 10 - (5 - X) * Work.Trade / 10 : 10;
                        b += 2 * routes;
                        if (b < 3)
                        {
                            b = 2;
                        }

                        b += Civ.ContinentTradeUnits[R];
                        if (Human && City.Flags.Autobuild)
                        {
                            continue;
                        }
                    }
                    b = Weight(b, d);
                    if (Has(BuildingIds.Barracks))
                    {
                        b += b / 4;
                    }

                    CompareBuild(t, b);
                }
            }
        }

        private bool Diplomat(int t, ref int b, ref int d)
        {
            unchecked
            {
                bool behind = SpaceshipQueries.RaceBehind(World, Rules, C);
                if (behind)
                {
                    if (Knows(AdvanceIds.SpaceFlight) && Knows((int)Rules.Improvements[BuildingIds.Component].Prerequisite)
                        && Knows((int)Rules.Improvements[BuildingIds.Module].Prerequisite) && Knows(AdvanceIds.FusionPower))
                    {
                        behind = false;
                    }

                    if (Urgent != 0 || City.Flags.Disorder || Difficulty < 2)
                    {
                        behind = false;
                    }
                }
                if (Human && City.Flags.Autobuild)
                {
                    return false;
                }

                int q = behind ? 1 : 0;
                if (queued[t] > q || present[t] >= RegionalCities)
                {
                    return true;
                }

                b = 11;
                int target = -1;
                for (int c = 1; c < EntityLimits.Civilizations; ++c)
                {
                    if (c == C || World.Civilizations[c].ContinentCities[R] == 0)
                    {
                        continue;
                    }

                    ref var other = ref World.Civilizations[c];
                    var treaty = Civ.Treaties[c];
                    if (!(treaty.Ceasefire || treaty.Peace) || World.Globals.HumanPlayers.Contains(c)
                        && (World.Globals.PowerRank[c] >= World.Globals.PowerRank[C] || behind)
                        && (t == UnitTypeIds.Spy || other.NumAdvances > Civ.NumAdvances + 7 || behind || World.Globals.PowerRank[c] == 7)
                        && (treaty.Alliance || behind || (byte)other.Government < (int)Contracts.Government.Republic
                            || other.ContinentAttack[R] <= Civ.ContinentMilitary[R] || RegionalCities == 0))
                    {
                        target = c;
                    }
                }
                if (target >= 0)
                {
                    b = Clamp(10 - (World.Civilizations[target].NumAdvances - Civ.NumAdvances), 2, 10);
                    // X01/A02: no ninth treaty; omit the invalid exhausted-cursor adjustment.
                    if (World.Civilizations[target].MilitaryStrength <= Civ.MilitaryStrength)
                    {
                        d = T;
                    }
                }
                else
                {
                    target = H;
                    if (P == 4 && Difficulty != 0)
                    {
                        d = T;
                        b = 8;
                        if (Active(t) + Queued(t) < (Civ.CityCount + 3) / 4)
                        {
                            if (World.Civilizations[H].Gold < Civ.Gold)
                            {
                                b = 7;
                            }

                            if (2 * World.Civilizations[H].Gold < Civ.Gold)
                            {
                                --b;
                            }
                        }
                        if (World.Civilizations[H].NumAdvances > Civ.NumAdvances)
                        {
                            if (present[t] + queued[t] == 0)
                            {
                                b -= 3;
                            }

                            b -= World.Civilizations[H].NumAdvances - Civ.NumAdvances;
                        }
                        b = Clamp(b, Active(t) + 1, 10);
                    }
                    if (behind)
                    {
                        b /= 2;
                    }

                    if (present[t] != 0)
                    {
                        b = 999;
                    }
                }
                if (Knows(AdvanceIds.Communism))
                {
                    --b;
                }

                if (behind && b < 20)
                {
                    for (int item = 35; item <= 37; ++item)
                    {
                        if (!Knows((int)Rules.Improvements[item].Prerequisite) && Knows(target, (int)Rules.Improvements[item].Prerequisite))
                        {
                            d = T;
                            b = Math.Min(b, 2 * queued[t] + 1);
                            if (queued[t] == 0 && Active(t) == 0)
                            {
                                --b;
                            }

                            if (choice is >= -37 and <= -35)
                            {
                                choice = t;
                            }
                        }
                    }
                }

                return true;
            }
        }

        private bool Prerequisite(int type) => Knows((int)Rules.Units[type].Prerequisite);

        private void OrdinaryUnits()
        {
            unchecked
            {
                for (int t = 0; t < RuleLimits.UnitTypes; ++t)
                {
                    var rule = Rules.Units[t];
                    int role = (sbyte)rule.Role;
                    if (role >= 6 || !Unit(t) || t == UnitTypeIds.Carrier)
                    {
                        continue;
                    }

                    if (role == 5 && (City.Size <= 1 || Work.SettlerFood > F && !(Work.SupportedSettlers == 0
                        && Active(UnitTypeIds.Settlers) + Active(UnitTypeIds.Engineers) == 0 && queued[UnitTypeIds.Settlers] + queued[UnitTypeIds.Engineers] < 2)))
                    {
                        continue;
                    }

                    if (t == UnitTypeIds.Warriors && Government >= (int)Contracts.Government.Monarchy)
                    {
                        continue;
                    }

                    if ((sbyte)rule.Attack >= 99 && (Active(t) >= 4 || Queued(t) >= 2))
                    {
                        continue;
                    }

                    if (role == 3 && (Active(UnitTypeIds.Fighter) + Queued(UnitTypeIds.Fighter) + Active(UnitTypeIds.StealthFighter) + Queued(UnitTypeIds.StealthFighter)
                        >= ForeignActive(UnitTypeIds.Bomber) + ForeignActive(UnitTypeIds.Helicopter) + ForeignActive(UnitTypeIds.StealthBomber) + 2
                        || Civ.Treaties[H].Peace && Active(t) + Queued(t) != 0))
                    {
                        continue;
                    }

                    if (Human && (t == UnitTypeIds.Warriors && Prerequisite(UnitTypeIds.Phalanx) || t == UnitTypeIds.Archers && Prerequisite(UnitTypeIds.Legion)
                        || t == UnitTypeIds.Horsemen && (Prerequisite(UnitTypeIds.Chariot) || Prerequisite(UnitTypeIds.Elephant))))
                    {
                        continue;
                    }

                    if (t == UnitTypeIds.Chariot && (Prerequisite(UnitTypeIds.Knights) || Prerequisite(UnitTypeIds.Crusaders))
                        || t == UnitTypeIds.Elephant && Prerequisite(UnitTypeIds.Knights))
                    {
                        continue;
                    }

                    int b;
                    if (rule.Domain == RuleDomain.Land)
                    {
                        b = 2 * queued[t] + present[t];
                        if (Human)
                        {
                            b /= 2;
                            if (t is UnitTypeIds.Marines or UnitTypeIds.AlpineTroops or UnitTypeIds.Partisans)
                            {
                                b += 2;
                            }
                        }
                        if (t == UnitTypeIds.Archers)
                        {
                            b += 2 * queued[UnitTypeIds.Legion] + present[UnitTypeIds.Legion];
                        }

                        if (A < 0)
                        {
                            if (t == UnitTypeIds.Knights)
                            {
                                b += 2 * queued[UnitTypeIds.Crusaders] + present[UnitTypeIds.Crusaders];
                            }
                        }
                        else if (t == UnitTypeIds.Crusaders)
                        {
                            b += 2 * queued[UnitTypeIds.Dragoons] + present[UnitTypeIds.Dragoons];
                        }

                        if (role == 0 && (sbyte)rule.Attack == 0)
                        {
                            if (P is 0 or 1 || 2 * queued[t] + present[t] > (P == 4 ? 1 : (World.Map.Regions[R].Land + 199) / 200) || Active(t) >= 2)
                            {
                                continue;
                            }
                        }
                        if (P == 4)
                        {
                            int add = 1 - X;
                            if (role != 5)
                            {
                                add *= 2;
                            }

                            if (Offshore != 0)
                            {
                                add /= 2;
                            }

                            b += add;
                            if (Offshore != 0 && role == 0)
                            {
                                int subtract = A + 1;
                                if (Prerequisite(UnitTypeIds.Transport))
                                {
                                    subtract *= 2;
                                }

                                b = Clamp(b - subtract, 0, 99);
                            }
                        }
                    }
                    else
                    {
                        if (Civ.ContinentMilitary[R] < (RegionalCities < 2 ? 4 : 8))
                        {
                            continue;
                        }

                        b = Div((Active(t) + 2 * Queued(t)) * Civ.ContinentMilitary[R], Ut + 1);
                        if (rule.Abilities.DetectSubmarines)
                        {
                            b -= ForeignActive(UnitTypeIds.Submarine);
                            if (b < 1)
                            {
                                b = 0;
                            }
                        }
                    }
                    if (rule.Abilities.Paradrop)
                    {
                        role = 0;
                    }

                    if (Need != 0 && (!Human || parent.preferences.NoDefend == 0))
                    {
                        if (role == 1)
                        {
                            b = Need == 2 && U < 2 ? -1 : Div(2, Need);
                        }
                        else if (Need == 2)
                        {
                            continue;
                        }
                    }
                    if (role is not 1 and not 5)
                    {
                        b *= P == role ? 2 : 4;
                    }

                    if (t == UnitTypeIds.Partisans && (Need != 0 || !Stronger))
                    {
                        continue;
                    }

                    if (rule.Domain == RuleDomain.Sea)
                    {
                        if (!City.Flags.Coastal)
                        {
                            continue;
                        }

                        int water = CityQueries.AdjacentSea(World, slot).LastRegion;
                        if (water is 0 or 63)
                        {
                            continue;
                        }

                        int borders = 0;
                        for (int land = 1; land < 63; ++land)
                        {
                            if (World.Map.Regions[land].NeighboringWater.Contains(water))
                            {
                                ++borders;
                            }
                        }

                        if (borders <= 1 && !(role != 4 && World.Map.Regions[water].Water > 47 && water < 63))
                        {
                            continue;
                        }
                    }
                    if (role == 4)
                    {
                        if (P == 4)
                        {
                            b = 3 * b / 2;
                        }
                        else if (P == 1 || Active(t) != 0)
                        {
                            continue;
                        }

                        if (t != UnitTypeIds.Transport)
                        {
                            b = (2 * b) << T;
                        }
                    }
                    if (role == 5)
                    {
                        b = Settler(t, b);
                    }
                    else
                    {
                        b *= 16;
                    }

                    b *= (sbyte)rule.CostRows + 2;
                    int attack = (sbyte)rule.Attack, defense = (sbyte)rule.Defense;
                    if (attack == 0 && defense == 0)
                    {
                        b = 0;
                    }
                    else
                    {
                        int hp = World.Globals.Rules.NormalCombat ? Clamp((sbyte)rule.HitPoints / 10 + (sbyte)rule.Firepower, 2, 4) : 2;
                        int combat = (attack + defense) * hp;
                        if (rule.Abilities.AntiHorseDefense)
                        {
                            ++combat;
                        }

                        if (t == UnitTypeIds.Partisans)
                        {
                            ++combat;
                        }

                        b = Div(b, (Div((sbyte)rule.Movement, Rules.Cosmic.RoadMovementMultiplier) + 1) * combat / 2);
                    }
                    if (role == 2)
                    {
                        if (World.Globals.Status.LargeMap)
                        {
                            b -= b / 4;
                        }

                        if (P == 4 && b > 0)
                        {
                            b -= b / 4;
                        }

                        b *= T == 0 ? 2 : 4;
                        if (Has(BuildingIds.PortFacility))
                        {
                            b -= b / 4;
                        }
                    }
                    if (rule.Domain == RuleDomain.Air)
                    {
                        b *= 2;
                        if (Has(BuildingIds.Airport))
                        {
                            b -= b / 4;
                        }
                    }
                    if (role == 1)
                    {
                        b -= (G + 1) >> 1;
                    }

                    if (Ut > 119)
                    {
                        b *= 4;
                    }

                    if (Need == 0 && role < 6)
                    {
                        int penalty = Work.ShieldSupport != 0 && Work.Shields != 0 ? Div((3 - T) * (Work.ShieldSupport + 1) * b, Work.Shields) : 0;
                        if (role == 5)
                        {
                            // Homeless remains zero under the exhausted-rule safety repair.
                            if (Active(t) + queued[t] == 0 && City.Size > 3 - X)
                            {
                                penalty = 0;
                                if (Work.Shields <= (X >= 0 ? 1 : 0) + 2 * Work.ShieldSupport)
                                {
                                    b -= b / 3;
                                }
                            }
                            else
                            {
                                penalty /= 2;
                            }
                        }
                        else if (T == 0 && Work.Shields <= Work.SupportedUnits || !Civ.Flags.AtWar)
                        {
                            b *= 2;
                        }

                        b += penalty;
                    }
                    if (Human && role == 1 && parent.preferences.NoDefend != 0)
                    {
                        b = 999;
                    }

                    if (b < choiceScore || choice < 0 && !City.Flags.Disorder && (Need > 1 || Urgent != 0 && U < 2 && Stronger))
                    { choice = t; choiceScore = b; }
                    if (b < bestUnitScore)
                    { bestUnit = t; bestUnitScore = b; }
                }
            }
        }

        private int Settler(int t, int b)
        {
            unchecked
            {
                int basis = Has(BuildingIds.Granary) || Wonder(WonderIds.Pyramids) ? 4 : 6;
                int trait = present[t] < (Government + 1) / 2 ? Z : X;
                b = (basis - trait - Expand) * b;
                for (int j = 0; j < Work.SupportedSettlers; ++j)
                {
                    b += (j + 1) * (City.Size < 4 ? 16 : 8);
                }

                if (City.Size < 2)
                {
                    b *= 2;
                }

                if (present[t] == 0 && Work.SettlerFood == F)
                {
                    b -= b / 2;
                }

                if (Need < 2)
                {
                    if (queued[t] == 0)
                    {
                        if (Government is >= (int)Contracts.Government.Monarchy and <= (int)Contracts.Government.Fundamentalism)
                        {
                            if (present[t] < Clamp(RegionalCities >> 1, 1, 3))
                            {
                                b /= 2;
                            }

                            if (present[t] < RegionalCities / 5)
                            {
                                b /= 2;
                            }
                        }
                        else if (Government >= (int)Contracts.Government.Republic)
                        {
                            if (present[t] < Clamp(RegionalCities - 1, 1, 6))
                            {
                                b /= 2;
                            }

                            if (present[t] < (RegionalCities >> 2))
                            {
                                b /= 2;
                            }
                        }
                    }
                    if (present[t] == 0 && queued[t] == 0)
                    {
                        b /= 2;
                        if (P != 4)
                        {
                            int threshold = X + (P == 1 ? 2 : 3);
                            if (P == 5)
                            {
                                ++threshold;
                            }

                            if (RegionalCities < threshold)
                            {
                                b = 0;
                            }
                        }
                    }
                }
                return b;
            }
        }
    }
}
