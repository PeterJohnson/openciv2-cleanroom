using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.City;

public sealed partial class CityEconomy
{
    public int TradeAndHappiness(CitySlot slot)
    {
        unchecked
        {
            int owner = Owner(slot);
            ref var city = ref world.Entities.Cities.At(slot);
            Work.TradeCorruption = Loss(slot, Work.Trade, false, false);
            if (GovernmentOf(owner) is (int)Government.Fundamentalism or (int)Government.Democracy)
            {
                Work.TradeCorruption = 0;
            }

            city.BaseTrade = (short)(Work.Trade - Work.TradeCorruption);
            Commodities.Refresh(world, Rules, slot);
            for (int i = 0; i < (sbyte)city.RouteCount; ++i)
            {
                var partner = city.RoutePartners[i] ?? throw new InvalidOperationException("Recurring trade requires a backed partner city");
                ref var other = ref world.Entities.Cities.At(partner);
                int value = (city.BaseTrade + other.BaseTrade + 4) >> 3;
                int commodity = city.RouteGoods[i];
                int connection = RoadConnections.Query(world, Rules, Owner(slot), city.Position, other.Position);
                if (Has(slot, BuildingIds.Airport) && Has(partner, BuildingIds.Airport) && connection < 2)
                {
                    connection = 1;
                }

                if (Has(slot, BuildingIds.Superhighways))
                {
                    ++connection;
                }

                if (connection != 0)
                {
                    value += (connection * value) >> 1;
                }

                if (commodity < 0)
                {
                    value = 0;
                }

                if ((sbyte)other.Owner.Value == owner)
                {
                    value >>= 1;
                }

                Work.RouteTrade[i] = value;
                Work.Trade += value;
            }
            Work.TradeCorruption = Loss(slot, Work.Trade, false, true);
            if (GovernmentOf(owner) is (int)Government.Fundamentalism or (int)Government.Democracy)
            {
                Work.TradeCorruption = 0;
            }

            int netTrade = Work.Trade - Work.TradeCorruption;
            Distribute(slot, netTrade, 0, 0);
            happiness.Calculate(slot, owner);
            city.Science = (short)Work.Science;
            city.Tax = (short)Work.Tax;
            city.Trade = (short)netTrade;
            city.TotalFood = (byte)Work.Food;
            city.TotalShields = (byte)Work.Shields;
            city.Happy = (byte)world.Mood.Happy;
            city.Unhappy = (byte)world.Mood.Unhappy;
            return world.Mood.Happy - world.Mood.Unhappy;
        }
    }

    public void Distribute(CitySlot slot, int inputTrade, int mode = 0, int extraTax = 0)
    {
        unchecked
        {
            ref var city = ref world.Entities.Cities.At(slot);
            int trade = mode == 1 && city.Production == -BuildingIds.Capitalization ? city.Trade : inputTrade;
            int owner = Owner(slot);
            ref var civ = ref world.Civilizations[owner];
            int scienceRate = civ.ScienceRate;
            if (GovernmentOf(owner) == (int)Government.Fundamentalism)
            {
                scienceRate = Math.Min(scienceRate, Rules.Cosmic.FundamentalismScienceCapTenths);
            }

            Work.Luxury = Arithmetic.ClampAB((trade * (10 - (civ.ScienceRate + civ.TaxRate)) + 4) / 10, 0, trade);
            Work.Science = Arithmetic.ClampAB((trade * scienceRate + 4) / 10, 0, trade - Work.Luxury);
            Work.Tax = trade - (Work.Science + Work.Luxury);
            if (Work.Processing)
            {
                Score(owner, Government.Fundamentalism, Work.Luxury / 2);
                Score(owner, Government.Fundamentalism, -((rules.Leaders[civ.Tribe].Civilize + 3) * Work.Science / 4));
            }
            if (!world.Globals.HumanPlayers.Contains(owner) && GovernmentOf(owner) == (int)Government.Fundamentalism)
            { Work.Science += Work.Luxury; Work.Luxury = 0; }
            Work.Luxury += 2 * working.CountSpecialists(slot, SpecialistKind.Entertainer);
            Work.Tax += 3 * working.CountSpecialists(slot, SpecialistKind.TaxCollector);
            Work.Science += 3 * working.CountSpecialists(slot, SpecialistKind.Scientist);
            if (GovernmentOf(owner) == (int)Government.Fundamentalism)
            {
                Work.Science -= Rules.Cosmic.FundamentalismScienceLossPercent * Work.Science / 100;
            }

            Work.BuildingContent = 0;
            if (Has(slot, BuildingIds.Temple))
            {
                if (Knows(owner, AdvanceIds.Mysticism))
                {
                    ++Work.BuildingContent;
                }

                if (Knows(owner, AdvanceIds.CeremonialBurial))
                {
                    ++Work.BuildingContent;
                }

                if (Wonder(owner, WonderIds.Oracle))
                {
                    Work.BuildingContent *= 2;
                }
            }
            if (Has(slot, BuildingIds.Colosseum))
            {
                Work.BuildingContent += 3;
                if (Knows(owner, AdvanceIds.Electronics))
                {
                    ++Work.BuildingContent;
                }
            }
            if (Knows(owner, AdvanceIds.Monotheism) && (Has(slot, BuildingIds.Cathedral) || Wonder(owner, WonderIds.Michelangelo)))
            {
                Work.BuildingContent += (Knows(owner, AdvanceIds.Communism) ? 0 : 1) + (Knows(owner, AdvanceIds.Theology) ? 3 : 2);
            }

            if (Wonder(owner, WonderIds.Bach))
            {
                Work.BuildingContent += 2;
            }

            if (GovernmentOf(owner) == (int)Government.Fundamentalism)
            {
                Work.Tax += Work.BuildingContent;
            }

            int level = Has(slot, BuildingIds.Marketplace) ? 1 : 0;
            if (Has(slot, BuildingIds.Bank))
            {
                ++level;
            }

            if (Has(slot, BuildingIds.StockExchange))
            {
                ++level;
            }

            Work.Luxury += (Work.Luxury * level) >> 1;
            Work.Tax += (Work.Tax * level) >> 1;
            if (Work.Processing)
            {
                Score(owner, Government.Fundamentalism, ((level + 2) * Work.BuildingContent) >> 1);
            }

            level = Has(slot, BuildingIds.Library) ? 1 : 0;
            if (Has(slot, BuildingIds.University))
            {
                ++level;
            }

            if (Has(slot, BuildingIds.ResearchLab) || Wonder(owner, WonderIds.Seti))
            {
                ++level;
            }

            int bonus = Work.Science * level;
            if (BuildingQueries.ActiveCity(world, Rules, WonderIds.Newton) != slot.Value)
            {
                bonus >>= 1;
            }

            Work.Science += bonus;
            if (BuildingQueries.ActiveCity(world, Rules, WonderIds.Copernicus) == slot.Value)
            {
                Work.Science *= 2;
            }

            if (city.Production == -BuildingIds.Capitalization)
            {
                if (mode == 0 && Work.ShieldSupport < Work.Shields)
                {
                    Work.Tax += Work.Shields - Work.ShieldSupport;
                }
                else if (mode == 1)
                {
                    Distribute(slot, inputTrade, 2, city.Tax - Work.Tax);
                    return;
                }
                else if (mode == 2)
                {
                    Work.Tax += extraTax;
                }
            }
            city.Science = (short)Work.Science;
            city.Tax = (short)Work.Tax;
            city.Trade = (short)trade;
        }
    }
}
