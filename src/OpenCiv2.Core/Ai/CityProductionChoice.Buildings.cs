using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Core.Ai;

public sealed partial class CityProductionChoice
{
    private sealed partial class Evaluation
    {
        private void Improvements()
        {
            unchecked
            {
                for (int item = 1; item <= BuildingIds.Capitalization; ++item)
                {
                    if (!Build(item))
                    {
                        continue;
                    }

                    int b = 999, d = 0, n = City.Size, h = F >> 1;
                    switch (item)
                    {
                        case BuildingIds.Palace:
                            if ((palaces == 0 || !Human) && (palaces == 0 || palaces == 1 && 2 * largestPalace < OldSize)
                                && (P is 4 or 5 or 0 || Work.ShieldWaste > 3))
                            {
                                b = 2;
                                if (OldSize > 9)
                                {
                                    b = 1;
                                }

                                if (OldSize > 14)
                                {
                                    --b;
                                }

                                if (Work.ShieldWaste > 3)
                                {
                                    d = T;
                                }
                            }
                            break;
                        case BuildingIds.Barracks:
                            b = Z + B + 4 - A;
                            if ((A < 0 || Z > 0) && Z + A != 0)
                            {
                                b += B;
                            }

                            d = 1;
                            if (Human)
                            {
                                if (Difficulty == 3)
                                {
                                    b = 3 * b / 2;
                                }
                                else if (Difficulty > 3)
                                {
                                    b *= 2;
                                }
                            }
                            break;
                        case BuildingIds.Granary:
                            if (!Wonder(WonderIds.Pyramids))
                            {
                                b = Work.Shields < 3 ? 8 : 4;
                                if (World.Mood.Happy <= World.Mood.Unhappy)
                                {
                                    b += 2;
                                }

                                if (Human)
                                {
                                    b += Math.Max(0, Difficulty + 4 - OldSize);
                                }
                            }
                            break;
                        case BuildingIds.Temple:
                            if (U == 1)
                            {
                                b = 9;
                            }
                            else if (U == 2)
                            {
                                b = -5;
                            }

                            break;
                        case BuildingIds.Marketplace:
                            b = Clamp(10 - (Q >> 1), 1, 10);
                            if (U == 1)
                            {
                                --b;
                            }
                            else if (U == 2)
                            {
                                b = -4;
                            }

                            break;
                        case BuildingIds.Library:
                            b = Clamp(10 - Q / 3, 1, 10);
                            break;
                        case BuildingIds.Courthouse:
                            b = 14 - (2 * Work.ShieldWaste + Work.TradeCorruption);
                            if (Civ.Government == Contracts.Government.Democracy)
                            {
                                if (U == 2)
                                {
                                    b = -1;
                                }
                            }
                            else if (!Human)
                            {
                                b -= World.Civilizations[H].SubversionCount / 2;
                            }

                            break;
                        case 8:
                            if (!Wonder(WonderIds.GreatWall))
                            {
                                b = 10 - (n >> 1);
                                for (int c = 1; c < EntityLimits.Civilizations; ++c)
                                {
                                    if (World.Globals.HumanPlayers.Contains(c))
                                    {
                                        if (Civ.Treaties[c].Vendetta || Civ.Treaties[c].War)
                                        {
                                            b -= 2;
                                        }

                                        if (World.Civilizations[c].ContinentCities[R] != 0)
                                        {
                                            --b;
                                        }
                                    }
                                }

                                foreach (int advance in new[] { AdvanceIds.Robotics, AdvanceIds.AdvancedFlight, AdvanceIds.Rocketry })
                                {
                                    if (World.Globals.AdvanceKnownBy[advance] != default)
                                    {
                                        b += 2;
                                    }
                                }

                                b = Clamp(b, 1, 10);
                                if (Has(BuildingIds.Palace))
                                {
                                    b -= 4;
                                }

                                d = 1;
                            }
                            break;
                        case BuildingIds.Aqueduct:
                            if (Rules.Cosmic.AqueductSize - h <= n)
                            {
                                b = Clamp(Rules.Cosmic.AqueductSize + 4 - n - h, 1, 20);
                            }

                            break;
                        case BuildingIds.Bank:
                            b = Clamp(10 - Q / 3, 1, 10);
                            if (U == 1)
                            {
                                --b;
                            }
                            else if (U == 2)
                            {
                                b = 0;
                            }

                            break;
                        case BuildingIds.Cathedral:
                            if (!Wonder(WonderIds.Michelangelo))
                            {
                                if (U == 1)
                                {
                                    b = 8;
                                }
                                else if (U == 2)
                                {
                                    b = -3;
                                }
                            }
                            break;
                        case BuildingIds.University:
                            if (Civ.ScienceRate != 0 && (Race == 0 || !Knows(AdvanceIds.SpaceFlight)))
                            {
                                b = Clamp(10 - (Q >> 2), 2, 10);
                            }

                            break;
                        case BuildingIds.Colosseum:
                            if (U == 2)
                            {
                                b = -2;
                            }

                            break;
                        case BuildingIds.Factory:
                        case BuildingIds.HydroPlant:
                            b = Clamp(14 - Work.Shields, 1, 14);
                            break;
                        case BuildingIds.ManufacturingPlant:
                        case BuildingIds.RecyclingCenter:
                        case BuildingIds.NuclearPlant:
                            b = Clamp(12 - Work.Shields / 4, 2, 12);
                            break;
                        case BuildingIds.SdiDefense:
                            if (U < 2)
                            {
                                b = (Clamp(15 - n, 1, 15) + 1) / 2;
                                if (World.Globals.WonderCity[WonderIds.Manhattan] == BuildingQueries.Unbuilt)
                                {
                                    b *= 2;
                                }

                                if (ForeignActive(UnitTypeIds.NuclearMissile) == 0)
                                {
                                    b *= 2;
                                }

                                if (n > 9)
                                {
                                    --b;
                                }

                                if (Has(BuildingIds.Palace))
                                {
                                    b = 0;
                                }
                            }
                            d = 1;
                            break;
                        case BuildingIds.PowerPlant:
                            b = Clamp(12 - Work.Shields / 5, 2, 14);
                            break;
                        case BuildingIds.StockExchange:
                            if (U != 0 || Race < 2)
                            {
                                b = Clamp(11 - (Q >> 2), 2, 11);
                                if (U == 1)
                                {
                                    --b;
                                }
                                else if (U == 2)
                                {
                                    b = 0;
                                }
                            }
                            break;
                        case BuildingIds.SewerSystem:
                            if (Race < 2 && Rules.Cosmic.SewerSize - h <= n)
                            {
                                b = Clamp(Rules.Cosmic.SewerSize + 4 - n - h, 1, 20);
                            }

                            break;
                        case BuildingIds.Supermarket:
                            b = Clamp(14 - (n >> 1) - 2 * Fm + 2 * Race + h, 2, 14);
                            break;
                        case BuildingIds.Superhighways:
                            b = Clamp(15 - (n >> 1) + 6 * Race, 2, 15);
                            break;
                        case BuildingIds.ResearchLab:
                            if ((Race == 0 || !Knows(AdvanceIds.SpaceFlight)) && Civ.ScienceRate != 0)
                            {
                                b = Clamp(11 - (Q >> 2), 2, 10);
                            }

                            break;
                        case 27:
                        case BuildingIds.CoastalFortress:
                            if (item == 27 || CityQueries.TouchesBody(World, slot, 1))
                            {
                                b = DefenseFacility(item == 27);
                                if (item == 27 && City.Flags.SamScoringInput)
                                {
                                    b -= 3;
                                }

                                d = 1;
                                for (int c = 1; c < EntityLimits.Civilizations; ++c)
                                {
                                    if (World.Globals.HumanPlayers.Contains(c) && (World.Civilizations[c].Reputation > 4 || Civ.Treaties[c].War))
                                    { d = T; break; }
                                }
                            }
                            break;
                        case BuildingIds.Harbor:
                            if (U < 2)
                            {
                                b = Clamp(16 - O, 2, 16);
                                if (F < 1)
                                {
                                    b /= 2;
                                }
                            }
                            break;
                        case BuildingIds.OffshorePlatform:
                            if (U < 2)
                            {
                                b = Clamp(16 - O, 2, 16);
                                if (b > 0)
                                {
                                    if (Has(BuildingIds.Factory))
                                    {
                                        b /= 2;
                                    }

                                    if (Has(BuildingIds.ManufacturingPlant))
                                    {
                                        b /= 2;
                                    }
                                }
                                if ((Work.Shields - Work.ShieldSupport <= Work.Shields / 3 || Work.Shields < OldSize / 2) && OldSize / 2 <= O / 4)
                                {
                                    b -= 2;
                                }
                            }
                            break;
                        case BuildingIds.Airport:
                            b = 4 * Ap + 4 - A;
                            if (Civ.ContinentPolicy[R] == 4)
                            {
                                b /= 2;
                            }

                            if (!Knows(AdvanceIds.AdvancedFlight))
                            {
                                b *= 2;
                            }

                            if ((RegionalCities == 1 || Stronger && Ap == 0) && RegionalCities < b)
                            {
                                b = RegionalCities;
                            }

                            if (Has(BuildingIds.Palace) && b >= 0)
                            {
                                b = 0;
                            }

                            d = T;
                            break;
                        case BuildingIds.PoliceStation:
                            if (!Wonder(WonderIds.WomensSuffrage))
                            {
                                int q = Work.AggressiveUnits;
                                if (Government > (int)Contracts.Government.Republic)
                                {
                                    q *= 2;
                                }

                                if (Government > (int)Contracts.Government.Fundamentalism && q != 0 && World.Mood.Happy - World.Mood.Unhappy < 2)
                                {
                                    q += 2;
                                }

                                b = 10 - q;
                            }
                            break;
                        case BuildingIds.PortFacility:
                            if (CityQueries.TouchesBody(World, slot, 1))
                            {
                                b = Clamp(11 - (Math.Min(D, 40) >> 2) - 5 * Naval + 2 * Pf - A + Z, 2, 15);
                                if (A < 0)
                                {
                                    b += Pf;
                                }
                            }
                            break;
                        case BuildingIds.Structural:
                            SpaceshipScore(ref b, ref d);
                            break;
                    }
                    b = Weight(b, d);
                    if (Has(BuildingIds.Barracks))
                    {
                        b += Div(b, (B >> 1) + 3);
                    }

                    if (BuildingQueries.ActiveCity(World, Rules, WonderIds.Shakespeare) == slot.Value)
                    {
                        b += b / 3;
                    }

                    CompareBuild(-item, b);
                }
            }
        }

        private int DefenseFacility(bool air)
        {
            int threat = 0;
            unchecked
            {
                if (!Human)
                {
                    threat = air ? 2 * (2 * ForeignActive(UnitTypeIds.StealthBomber) + 2 * ForeignActive(UnitTypeIds.Bomber) + ForeignActive(UnitTypeIds.CruiseMissile) + ForeignActive(UnitTypeIds.Helicopter))
                        : 3 * ForeignActive(UnitTypeIds.Cruiser) + 3 * ForeignActive(UnitTypeIds.AegisCruiser) + 5 * ForeignActive(UnitTypeIds.Battleship) + ForeignActive(UnitTypeIds.Ironclad) + ForeignActive(UnitTypeIds.Destroyer);
                }
                else
                {
                    for (int c = 1; c < EntityLimits.Civilizations; ++c)
                    {
                        if (c == C)
                        {
                            continue;
                        }

                        ref var other = ref World.Civilizations[c];
                        int subtotal = air ? other.UnitsActive[UnitTypeIds.CruiseMissile] + other.UnitsActive[UnitTypeIds.Bomber] + other.UnitsActive[UnitTypeIds.Helicopter] + other.UnitsActive[UnitTypeIds.StealthBomber]
                            : other.UnitsActive[UnitTypeIds.Ironclad] / 2 + other.UnitsActive[UnitTypeIds.Destroyer] / 2 + 2 * other.UnitsActive[UnitTypeIds.Battleship] + other.UnitsActive[UnitTypeIds.Cruiser] + other.UnitsActive[UnitTypeIds.AegisCruiser];
                        if (!Civ.Treaties[c].War && !other.Treaties[C].Vendetta)
                        {
                            subtotal /= 2;
                        }

                        threat += subtotal;
                    }
                }

                return Clamp(12 - threat, 1, 12);
            }
        }

        private void SpaceshipScore(ref int b, ref int d)
        {
            if (Civ.SpaceshipFlags.Launched || World.Globals.Status.Won || parent.spaceship.ChooseGroup(C, 0) < 0 || Human && Civ.SpaceshipParts[0] >= 38)
            {
                return;
            }

            b = 4;
            for (int c = 1; c < EntityLimits.Civilizations; ++c)
            {
                if (World.Globals.HumanPlayers.Contains(c) && World.Civilizations[c].SpaceshipFlags.Exists)
                { b = 2; break; }
            }

            if (P != 0 && !Stronger)
            {
                d = T;
            }

            bool launched = false;
            if (!World.Globals.Status.Won)
            {
                for (int c = 1; c < EntityLimits.Civilizations; ++c)
                {
                    if (World.Globals.HumanPlayers.Contains(c) && World.Civilizations[c].SpaceshipFlags.Launched)
                    { launched = true; break; }
                }
            }

            if (Race > 1 && Difficulty != 0)
            {
                b -= launched ? 2 : 1;
                if (Urgent == 0)
                {
                    d = T;
                }
            }
            int ownCost = 0, leadingCost = 0;
            for (int part = 0; part < 6; ++part)
            {
                int group = Clamp((part + 1) / 2, 0, 2);
                ownCost = unchecked(ownCost + Civ.SpaceshipParts[part] * Rules.Improvements[35 + group].CostRows);
                leadingCost = unchecked(leadingCost + World.Civilizations[H].SpaceshipParts[part] * Rules.Improvements[35 + group].CostRows);
            }
            if ((Sb == 0 || Sb <= Hsb && ownCost <= leadingCost || launched) && !City.Flags.Disorder && Difficulty != 0
                && ((Civ.BestShieldSurplus >> 1) <= S || S > 19))
            {
                choiceScore = 999;
            }
        }
    }
}
