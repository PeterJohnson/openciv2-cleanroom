using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>C03 full local city recalculation with real C02/C04/C05/C07/C08 and U03/W05 children.
/// This computes city state; it does not advance stocks, pay revenue or run city-turn consequences.</summary>
public sealed partial class CityEconomy
{
    private readonly GameState world;
    private readonly RuleState rules;
    private readonly CityWorking working;
    private readonly CitySupport support;
    private readonly CityHappiness happiness;
    private RuleTables Rules => rules.Definitions;
    private CityCalculationWorkspace Work => world.Calculation;
    private bool Has(CitySlot city, int building) => BuildingQueries.HasBuilding(world, city, building);
    private bool Wonder(int owner, int wonder) => BuildingQueries.HasWonder(world, Rules, owner, wonder);
    private bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
    private int Owner(CitySlot city) => unchecked((sbyte)world.Entities.Cities.At(city).Owner.Value);
    private int GovernmentOf(int owner) => unchecked((byte)world.Civilizations[owner].Government);

    public CityEconomy(GameState world, RuleState rules, StackOperations stacks)
    {
        this.world = world;
        this.rules = rules;
        working = new(world, rules.Definitions, stacks);
        support = new(world, rules.Definitions);
        happiness = new(world, rules.Definitions, stacks);
    }

    public int Calculate(CitySlot? city, bool full = true) => world.Mood.Calculate(city, full, () =>
    {
        MapKnowledge.RequireLocal(world);
        var slot = city!.Value;
        working.RefreshAvailability(slot);
        CapitalAndOverlap(slot);
        support.Calculate(slot);
        return FinalStats(slot);
    });

    /// <summary>Steps 4–8 of C03, using existing availability, capital and support state without selecting a cached city.</summary>
    public int FinalStats(CitySlot city)
    {
        MapKnowledge.RequireLocal(world);
        working.Assign(city);
        FoodRoutes(city);
        working.FillSpecialists(city);
        ProductionPollutionAndWaste(city);
        return TradeAndHappiness(city);
    }

    public void CapitalAndOverlap(CitySlot slot)
    {
        var origin = world.Entities.Cities.At(slot).Position;
        var map = world.Map;
        bool flat = world.Globals.Rules.FlatWorld;
        Work.CapitalDistance = Has(slot, BuildingIds.Palace) ? 0 : 32;
        Work.CapitalCity = null;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            if (i == slot.Value)
            {
                continue;
            }

            ref var candidate = ref world.Entities.Cities.Storage[i];
            if (!candidate.Token.Occupied)
            {
                continue;
            }

            int distance = map.Geometry.WeightedDistance(candidate.Position, world.Entities.Cities.At(slot).Position, flat);
            if (candidate.Owner == world.Entities.Cities.At(slot).Owner && Has(new((ushort)i), BuildingIds.Palace))
            {
                if (distance < Work.CapitalDistance || Work.CapitalCity is null)
                {
                    Work.CapitalCity = new((ushort)i);
                }

                if (distance < Work.CapitalDistance)
                {
                    Work.CapitalDistance = distance;
                }
            }
            if (distance >= 6)
            {
                continue;
            }

            for (int p = 0; p <= 20; ++p)
            {
                if (p != 20 && !candidate.WorkedTiles.Contains(p))
                {
                    continue;
                }

                var offset = MapNeighborhoods.City[p];
                var point = new MapPoint(unchecked(candidate.Position.X + offset.X), unchecked(candidate.Position.Y + offset.Y));
                if (map.Geometry.WeightedDistance(origin, point, flat) >= 3)
                {
                    continue;
                }

                var delta = new MapPoint(unchecked(point.X - origin.X), unchecked(point.Y - origin.Y));
                for (int q = 0; q < 20; ++q)
                {
                    if (MapNeighborhoods.City[q] == delta)
                    {
                        Work.Availability[q] |= TileAvailability.OtherCityWorked;
                    }
                }
            }
        }
        Work.CapitalLink = Work.CapitalDistance == 0 ? 1 : 0;
        if (Work.CapitalLink != 0 && Knows(Owner(slot), AdvanceIds.Railroad))
        {
            ++Work.CapitalLink;
        }

        if (Work.CapitalCity is { } capital && capital != slot)
        {
            if (Has(slot, BuildingIds.Airport) && Has(capital, BuildingIds.Airport))
            {
                Work.CapitalLink = 1;
            }

            var target = world.Entities.Cities.At(capital).Position;
            if (map.TileAt(origin).Region == map.TileAt(target).Region)
            {
                Work.CapitalLink = RoadConnections.Query(world, Rules, Owner(slot), origin, target);
            }
        }
    }

    public void FoodRoutes(CitySlot slot)
    {
        Work.FoodOut = Work.FoodIn = 0;
        Work.FoodPartner = null;
        ref var city = ref world.Entities.Cities.At(slot);
        for (int i = 0; i < unchecked((sbyte)city.RouteCount); ++i)
        {
            if (city.RouteGoods[i] < 0)
            { ++Work.FoodOut; Work.FoodPartner = city.RoutePartners[i]; }
        }

        Work.Food = unchecked(Work.Food - Work.FoodOut);
        foreach (ref readonly var incoming in world.Entities.Cities.Scanned)
        {
            if (!incoming.Token.Occupied)
            {
                continue;
            }

            for (int i = 0; i < unchecked((sbyte)incoming.RouteCount); ++i)
            {
                if (incoming.RoutePartners[i] == slot && incoming.RouteGoods[i] < 0)
                { Work.Food = unchecked(Work.Food + 1); ++Work.FoodIn; }
            }
        }
    }

    public static int LossDivisor(int government) => government switch { <= 0 => 4, 1 => 5, 2 => 6, 3 or 4 => 7, _ => 8 };

    public int Loss(CitySlot city, int amount, bool capDistance, bool updateScores)
    {
        if (amount < 1)
        {
            return 0;
        }

        int owner = Owner(city), government = GovernmentOf(owner);
        bool communist = government == (int)Government.Communism;
        if (world.Entities.Cities.At(city).Flags.Celebrating)
        {
            ++government;
        }

        int distance = capDistance ? Math.Min(Work.CapitalDistance, 16) : Work.CapitalDistance;
        int communistLoss = unchecked(Rules.Cosmic.CommunismPalaceDistance * amount * 3) / (LossDivisor(government) * 20);
        int loss = Arithmetic.ClampAB(communist ? communistLoss : DistanceLoss(government), 0, amount);
        if (Has(city, BuildingIds.Courthouse) || Has(city, BuildingIds.Palace))
        {
            loss >>= 1;
        }

        if (Work.Processing && updateScores)
        {
            for (int candidate = 1; candidate <= 5; ++candidate)
            {
                if (candidate == (int)Government.Fundamentalism)
                {
                    continue;
                }

                int raw = candidate == (int)Government.Communism ? communistLoss
                    : DistanceLoss(candidate + (world.Entities.Cities.At(city).Flags.Celebrating ? 1 : 0));
                Score(owner, (Government)candidate, unchecked(-raw));
            }
        }
        return loss;

        int DistanceLoss(int g)
        {
            int d = distance;
            if (g < 2 && d != 0 && world.Globals.HumanPlayers.Contains(owner))
            {
                d = Arithmetic.ClampAB(unchecked(d + unchecked((byte)world.Globals.Difficulty)), 0, 32);
            }

            return unchecked(d * amount * 3) / (LossDivisor(g) * 20);
        }
    }

    private void Score(int owner, Government government, int delta)
    {
        ref var score = ref world.Civilizations[owner].GovernmentScore[(int)government];
        score = unchecked((short)(score + delta));
    }

    public void ProductionPollutionAndWaste(CitySlot city)
    {
        int owner = Owner(city), factory = 0, power = 0;
        Work.PollutionDivisor = 1;
        if (Has(city, BuildingIds.Factory))
        {
            factory = 2;
        }

        if (Has(city, BuildingIds.ManufacturingPlant))
        {
            factory += 2;
        }

        if (Has(city, BuildingIds.PowerPlant))
        {
            power = 2;
        }

        if (Has(city, BuildingIds.HydroPlant) || Has(city, BuildingIds.NuclearPlant) || Wonder(Owner(city), WonderIds.HooverDam))
        { power = 2; Work.PollutionDivisor = 2; }
        if (Has(city, BuildingIds.RecyclingCenter))
        {
            Work.PollutionDivisor = 3;
        }

        if (Has(city, BuildingIds.SolarPlant))
        { power = 2; Work.PollutionDivisor = 3; }
        power = Math.Min(power, factory);
        int shields = Work.Shields;
        Work.Shields = unchecked(shields + ((shields * factory) >> 2) + ((shields * power) >> 2));
        PollutionIntensity(city, owner);
        int raw = Loss(city, unchecked(Work.Shields - Work.ShieldSupport), true, true);
        int waste = unchecked((3 - Work.CapitalLink) * raw) / 3;
        waste /= (GovernmentOf(owner) >> 1) + 1;
        Work.ShieldWaste = Math.Max(0, Math.Min(waste, unchecked(Work.Shields - (Work.ShieldSupport + 1))));
        if (owner == 0 || GovernmentOf(owner) is (int)Government.Democracy or (int)Government.Fundamentalism
            || world.Entities.Cities.At(city).Production == -BuildingIds.Palace && Work.CapitalCity is null)
        {
            Work.ShieldWaste = 0;
        }

        Work.Shields = unchecked(Work.Shields - Work.ShieldWaste);
    }

    private void PollutionIntensity(CitySlot city, int owner)
    {
        Work.PopulationFactor = 0;
        if (!Has(city, BuildingIds.MassTransit))
        {
            foreach (int advance in new[] { AdvanceIds.Industrialization, AdvanceIds.Automobile, AdvanceIds.MassProduction, AdvanceIds.Plastics })
            {
                if (Knows(owner, advance))
                {
                    ++Work.PopulationFactor;
                }
            }

            if (Work.PopulationFactor != 0 && !Knows(owner, AdvanceIds.Sanitation))
            {
                ++Work.PopulationFactor;
            }

            if (Work.PopulationFactor != 0 && Knows(owner, AdvanceIds.Environmentalism))
            {
                --Work.PopulationFactor;
            }

            if (Work.PopulationFactor != 0 && Has(city, BuildingIds.SolarPlant))
            {
                --Work.PopulationFactor;
            }
        }
        int pollution = unchecked(Work.Shields / Work.PollutionDivisor - 20);
        if (Has(city, BuildingIds.SolarPlant))
        {
            pollution = 0;
        }

        Work.Pollution = unchecked(pollution + ((world.Entities.Cities.At(city).Size * Work.PopulationFactor) >> 2));
    }
}
