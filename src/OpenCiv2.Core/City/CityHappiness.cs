using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>C04's five calculation stages and whole-counter normalization. Disorder, tax collection,
/// celebration and government transitions belong to the later city-turn operation.</summary>
public sealed class CityHappiness(GameState world, RuleTables rules, StackOperations stacks)
{
    private CityCalculationWorkspace Work => world.Calculation;
    private CityMoodWorkspace Mood => world.Mood;
    private bool Has(CitySlot city, int building) => BuildingQueries.HasBuilding(world, city, building);
    private bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
    private bool Wonder(int owner, int wonder) => BuildingQueries.HasWonder(world, rules, owner, wonder);
    private int GovernmentOf(int owner) => unchecked((byte)world.Civilizations[owner].Government);

    public void Normalize(CitySlot slot, int stage)
    {
        unchecked
        {
            ref var city = ref world.Entities.Cities.At(slot);
            Mood.Happy = Arithmetic.ClampAB(Mood.Happy, 0, city.Size);
            while (Work.ExcessUnhappy != 0 && Mood.Unhappy < Work.ExcessUnhappy)
            { --Work.ExcessUnhappy; ++Mood.Unhappy; }
            Mood.Unhappy = Arithmetic.ClampAB(Mood.Unhappy, 0, city.Size);
            while (Mood.Unhappy + Mood.Happy > Arithmetic.ClampAB(city.Size - Work.Unassigned, 0, 99))
            {
                if (Work.ExcessUnhappy == 0)
                {
                    Mood.Happy = Arithmetic.ClampAB(Mood.Happy - 1, 0, city.Size);
                }
                else
                {
                    --Work.ExcessUnhappy;
                }

                Mood.Unhappy = Arithmetic.ClampAB(Mood.Unhappy - 1, 0, city.Size);
            }
            while (Work.ExcessUnhappy != 0 && Mood.Unhappy + Mood.Happy < city.Size - Work.Unassigned)
            { --Work.ExcessUnhappy; ++Mood.Unhappy; }
            if (stage >= 0)
            {
                Work.HistoryHappy[stage] = (byte)Mood.Happy;
                Work.HistoryUnhappy[stage] = (byte)Mood.Unhappy;
                Work.HistoryExcess[stage] = (byte)Work.ExcessUnhappy;
            }
        }
    }

    public void Calculate(CitySlot slot, int owner)
    {
        unchecked
        {
            ref var city = ref world.Entities.Cities.At(slot);
            Mood.Happy = 0;
            if (!world.Globals.HumanPlayers.Contains(owner))
            {
                Mood.Unhappy = city.Size - 1 - (rules.Cosmic.UnhappinessCitySize - 5);
            }
            else
            {
                int difficulty = (byte)world.Globals.Difficulty;
                int factor = rules.Cosmic.UnhappinessEmpireSize - 2 * difficulty;
                if (world.Globals.Status.LargeMap)
                {
                    factor += 2;
                }

                int divisor = ((GovernmentOf(owner) >> 1) + 2) * factor / 2;
                if (divisor < 2)
                {
                    divisor = 1;
                }

                int content = rules.Cosmic.UnhappinessCitySize - difficulty;
                if (content < 3 && Has(slot, BuildingIds.Palace) && Work.AggressiveUnits == 0 && Work.HomeAttackUnits == 0)
                {
                    content = 2;
                }

                Mood.Unhappy = city.Size - 1 - (content - 2);
                if (GovernmentOf(owner) != (int)Government.Communism)
                {
                    Mood.Unhappy += (world.Civilizations[owner].CityCount - divisor + slot.Value % divisor) / divisor;
                }
            }
            Work.ExcessUnhappy = 0;
            if (city.Size < Mood.Unhappy)
            { Work.ExcessUnhappy = Mood.Unhappy - city.Size; Mood.Unhappy = city.Size; }
            Normalize(slot, 0);
            Mood.Happy = Work.Luxury >> 1;
            Normalize(slot, 1);
            if (Has(slot, BuildingIds.Colosseum))
            {
                Mood.Unhappy -= 3;
                if (Knows(owner, AdvanceIds.Electronics))
                {
                    --Mood.Unhappy;
                }
            }
            if (Knows(owner, AdvanceIds.Monotheism) && (Has(slot, BuildingIds.Cathedral) || Wonder(owner, WonderIds.Michelangelo)))
            {
                Mood.Unhappy -= (Knows(owner, AdvanceIds.Communism) ? 0 : 1) + (Knows(owner, AdvanceIds.Theology) ? 3 : 2);
            }

            if (Has(slot, BuildingIds.Temple))
            {
                int content = Knows(owner, AdvanceIds.Mysticism) ? 1 : 0;
                if (Knows(owner, AdvanceIds.CeremonialBurial))
                {
                    ++content;
                }

                if (Wonder(owner, WonderIds.Oracle))
                {
                    content *= 2;
                }

                Mood.Unhappy -= content;
            }
            if ((Has(slot, BuildingIds.Courthouse) || Has(slot, BuildingIds.Palace)) && GovernmentOf(owner) == (int)Government.Democracy)
            {
                ++Mood.Happy;
            }

            Normalize(slot, 2);
            if (GovernmentOf(owner) == (int)Government.Fundamentalism)
            {
                Work.ExcessUnhappy = Mood.Unhappy = 0;
            }
            else if (GovernmentOf(owner) < (int)Government.Republic)
            {
                Work.MartialLaw = 0;
                for (int unit = stacks.FirstAt(city.Position); unit >= 0; unit = stacks.Next(unit))
                {
                    if (rules.Units[world.Entities.Units.Storage[unit].Type.Value].Attack != 0)
                    {
                        Work.MartialLaw += GovernmentOf(owner) == (int)Government.Communism ? 2 : 1;
                    }
                }

                Work.MartialLaw = Math.Min(Work.MartialLaw, GovernmentOf(owner) == (int)Government.Communism ? 6 : 3);
                Mood.Unhappy -= Arithmetic.ClampAB(Work.MartialLaw, 0, Mood.Unhappy);
            }
            else
            {
                int multiplier = Wonder(owner, WonderIds.WomensSuffrage) || Has(slot, BuildingIds.PoliceStation) ? 0 : 1;
                if (GovernmentOf(owner) == (int)Government.Democracy)
                {
                    ++multiplier;
                }

                if (multiplier != 0)
                {
                    int n = Work.AggressiveUnits;
                    if (n != 0 && GovernmentOf(owner) == (int)Government.Republic)
                    {
                        --n;
                    }

                    Mood.Unhappy += multiplier * n;
                }
            }
            Normalize(slot, 3);
            if (Wonder(owner, WonderIds.HangingGardens))
            {
                ++Mood.Happy;
                if (BuildingQueries.ActiveCity(world, rules, WonderIds.HangingGardens) == slot.Value)
                {
                    Mood.Happy += 2;
                }
            }
            if (Wonder(owner, WonderIds.CureForCancer))
            {
                ++Mood.Happy;
            }

            if (BuildingQueries.ActiveCity(world, rules, WonderIds.Shakespeare) == slot.Value)
            {
                Mood.Unhappy = 0;
            }

            if (Wonder(owner, WonderIds.Bach))
            {
                Mood.Unhappy -= 2;
            }

            Normalize(slot, 4);
        }
    }
}
