using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static CityEconomy Economy(RevelationFixture f) => new(f.World, f.Process.Rules, f.Stacks);

    private static void CityLossPublishedExamples()
    {
        var f = WorkingFixture(); var e = Economy(f); var w = f.World.Calculation;
        f.World.Civilizations[1].Government = Government.Despotism; f.World.Globals.Difficulty = Difficulty.King;
        w.CapitalDistance = 32;
        Equal(96, e.Loss(new(0), 100, false, false)); Equal(57, e.Loss(new(0), 100, true, false));
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Courthouse, true);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Palace, true);
        w.Processing = true;
        Equal(28, e.Loss(new(0), 100, true, true)); // Buildings halve once; scores use raw losses.
        Equal((short)-57, f.World.Civilizations[1].GovernmentScore[(int)Government.Despotism]);
        Equal((short)-40, f.World.Civilizations[1].GovernmentScore[(int)Government.Monarchy]);
        Equal((short)0, f.World.Civilizations[1].GovernmentScore[(int)Government.Fundamentalism]);
        Equal(0, e.Loss(new(60000), 0, true, true)); // No city read for nonpositive amount.
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Courthouse, false);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Palace, false);
        f.World.Civilizations[1].Government = Government.Communism;
        f.Rules.Cosmic = f.Rules.Cosmic with { CommunismPalaceDistance = 7 };
        Equal(15, e.Loss(new(0), 100, false, false));
        f.World.Entities.Cities.Storage[0].Flags.Celebrating = true;
        Equal(15, e.Loss(new(0), 100, false, false));
        f.World.Civilizations[1].Government = Government.Democracy;
        Equal(60, e.Loss(new(0), 100, false, false)); // Immunity is the caller's responsibility.
        w.CapitalDistance = -5;
        Equal(0, e.Loss(new(0), 100, false, true));
        f.World.Civilizations[1].Government = (Government)257; // Unsigned-byte government view is Despotism.
        f.World.Entities.Cities.Storage[0].Flags.Celebrating = false;
        w.CapitalDistance = 32;
        Equal(96, e.Loss(new(0), 100, false, false));
    }

    private static void CityIndustrialPollutionWaste()
    {
        var f = WorkingFixture(); var e = Economy(f); var w = f.World.Calculation;
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Factory, true);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.PowerPlant, true);
        w.Shields = 3; e.ProductionPollutionAndWaste(new(0)); Equal(5, w.Shields); Equal(-15, w.Pollution);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.ManufacturingPlant, true);
        w.Shields = 41; w.ShieldSupport = 4; w.CapitalDistance = 32; w.CapitalLink = 0;
        e.ProductionPollutionAndWaste(new(0));
        Equal(82, w.Pollution); Equal(19, w.ShieldWaste); Equal(83, w.Shields); // Published C03 example.
        f.World.Civilizations[1].Government = Government.Democracy;
        w.Processing = true; w.Shields = 41;
        e.ProductionPollutionAndWaste(new(0));
        Equal(0, w.ShieldWaste); Equal(102, w.Shields);
        Equal(true, f.World.Civilizations[1].GovernmentScore[(int)Government.Despotism] < 0); // Immunity did not undo scores.
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Industrialization).With(AdvanceIds.Environmentalism);
        f.World.Entities.Cities.Storage[0].Size = 12;
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.SolarPlant, true);
        w.Shields = 41; e.ProductionPollutionAndWaste(new(0));
        Equal(0, w.PopulationFactor); Equal(0, w.Pollution); Equal(3, w.PollutionDivisor);
        f.World.Civilizations[1].Advances = f.World.Civilizations[1].Advances.With(AdvanceIds.Automobile);
        w.Shields = 41; e.ProductionPollutionAndWaste(new(0)); Equal(1, w.PopulationFactor); Equal(3, w.Pollution);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.SolarPlant, false);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.MassTransit, true);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.HydroPlant, true);
        w.Shields = 41; e.ProductionPollutionAndWaste(new(0)); Equal(0, w.PopulationFactor); Equal(31, w.Pollution);
        f.World.Civilizations[1].Government = Government.Monarchy;
        f.World.Entities.Cities.Storage[0].Production = -BuildingIds.Palace;
        w.CapitalCity = null; w.Shields = 41; e.ProductionPollutionAndWaste(new(0)); Equal(0, w.ShieldWaste);
        w.CapitalCity = new(1); w.Shields = 41; e.ProductionPollutionAndWaste(new(0)); Equal(19, w.ShieldWaste);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.HydroPlant, false);
        w.Shields = int.MinValue;
        e.ProductionPollutionAndWaste(new(0));
        Equal(int.MinValue, w.Shields); Equal(2147483628, w.Pollution); // Products and pollution subtraction retain signed32 wrap.
    }

    private static void CityCapitalAndRawOverlap()
    {
        var f = WorkingFixture(); var e = Economy(f); var w = f.World.Calculation;
        f.AddCity(1, 2, new(10, 6));
        w.Availability[1] = TileAvailability.HumanBlocker;
        e.CapitalAndOverlap(new(0));
        Equal(TileAvailability.HumanBlocker | TileAvailability.OtherCityWorked, w.Availability[1]);
        Equal(TileAvailability.None, w.Availability[20]); Equal((CitySlot?)null, w.CapitalCity);
        f.World.Globals.Rules.FlatWorld = false;
        f.World.Entities.Cities.Storage[0].Position = new(0, 6);
        f.World.Entities.Cities.Storage[1].Position = new(18, 6);
        Array.Clear(w.Availability);
        e.CapitalAndOverlap(new(0));
        Equal(TileAvailability.None, w.Availability[5]); // Wrapped equality is not a raw radius offset match.
        f.World.Map.Allocate(new(120, 24));
        f.World.Map.Tiles.Fill(new() { Terrain = Terrain.Plains, Region = 1 });
        f.World.Globals.Rules.FlatWorld = true;
        f.World.Entities.Cities.Storage[0].Position = new(8, 6);
        f.World.Entities.Cities.Storage[1].Owner = new(1); f.World.Entities.Cities.Storage[1].Position = new(100, 6);
        BuildingQueries.SetBuilding(f.World, new(1), BuildingIds.Palace, true);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Airport, true);
        BuildingQueries.SetBuilding(f.World, new(1), BuildingIds.Airport, true);
        e.CapitalAndOverlap(new(0));
        Equal((CitySlot?)new CitySlot(1), w.CapitalCity); Equal(32, w.CapitalDistance); Equal(0, w.CapitalLink); // Road query overwrote airport.
        f.World.Map.TileAt(new(100, 6)).Region = 2;
        e.CapitalAndOverlap(new(0)); Equal(1, w.CapitalLink);
        f.AddCity(2, 1, new(20, 6)); BuildingQueries.SetBuilding(f.World, new(2), BuildingIds.Palace, true);
        f.AddCity(3, 1, new(8, 18)); BuildingQueries.SetBuilding(f.World, new(3), BuildingIds.Palace, true);
        e.CapitalAndOverlap(new(0)); Equal((CitySlot?)new CitySlot(2), w.CapitalCity); Equal(9, w.CapitalDistance); // First equal-distance palace.
    }

    private static void CityRoadConnectionSemantics()
    {
        var f = WorkingFixture(); var w = f.World.Paths;
        var a = new MapPoint(8, 6); var b = new MapPoint(12, 6); var middle = new MapPoint(10, 6);
        Equal(0, RoadConnections.Query(f.World, f.Rules, 1, a, b));
        f.World.Map.TileAt(middle).Features = MapFeatures.Road;
        Equal(1, RoadConnections.Query(f.World, f.Rules, 1, a, b));
        f.World.Map.TileAt(middle).Features |= MapFeatures.Railroad;
        Equal(2, RoadConnections.Query(f.World, f.Rules, 1, a, b));
        Equal(-1, w.Owner); Equal(false, w.UniformCost || w.RoadsOnly); Equal(0, w.ResultCost); // Goal cache reused.
        f.World.Map.TileAt(middle).Features = MapFeatures.CitySite;
        f.World.Map.TileAt(middle).Owner = 1;
        Equal(2, RoadConnections.Query(f.World, f.Rules, 1, a, b)); // Early own city needs no roads.
        f.World.Map.TileAt(middle).Owner = 2;
        Equal(0, RoadConnections.Query(f.World, f.Rules, 1, a, b));
        f.World.Civilizations[1].Treaties[2].Alliance = true;
        f.World.Map.TileAt(middle).Features |= MapFeatures.Road;
        Equal(1, RoadConnections.Query(f.World, f.Rules, 1, a, b));
        f.World.Map.TileAt(middle).Features = MapFeatures.Unit | MapFeatures.Road | MapFeatures.Railroad;
        f.World.Map.TileAt(middle).Owner = 0;
        Equal(2, RoadConnections.Query(f.World, f.Rules, 1, a, b)); // Barbarian units do not block the second owner test.
        f.World.Map.TileAt(b).Region = 2;
        w.UniformCost = w.RoadsOnly = true; w.Owner = 7;
        Equal(0, RoadConnections.Query(f.World, f.Rules, 1, a, b));
        Equal(true, w.UniformCost && w.RoadsOnly); Equal(7, w.Owner); // Early rejection preserves path state.
        f.World.Map.TileAt(b).Region = 1;
        Equal(2, RoadConnections.Query(f.World, f.Rules, 1, a, middle)); // Endpoint precedes ownership/infrastructure gates.
    }

    private static void CityFoodRouteMultiplicity()
    {
        var f = WorkingFixture(); var e = Economy(f); var w = f.World.Calculation;
        ref var city = ref f.World.Entities.Cities.Storage[0];
        city.RouteCount = 2; city.RouteGoods[0] = city.RouteGoods[1] = -1;
        city.RoutePartners[0] = new(0); city.RoutePartners[1] = new(88);
        f.AddCity(1, 2, new(14, 6));
        ref var other = ref f.World.Entities.Cities.Storage[1];
        other.RouteCount = 3;
        for (int i = 0; i < 3; ++i) { other.RouteGoods[i] = -1; other.RoutePartners[i] = new(0); }
        f.AddCity(2, 2, new(16, 8));
        f.World.Entities.Cities.Storage[2] = other with { Token = default };
        w.Food = 4; city.FoodBox = -1;
        e.FoodRoutes(new(0));
        Equal(2, w.FoodOut); Equal(4, w.FoodIn); Equal(6, w.Food); Equal((CitySlot?)new CitySlot(88), w.FoodPartner);
        Equal((short)-1, city.FoodBox); // No recomputation of the prior shortage marker.
        city.RouteCount = 255; other.RouteCount = 255; w.Food = 3;
        e.FoodRoutes(new(0)); Equal(3, w.Food); Equal(0, w.FoodIn); Equal((CitySlot?)null, w.FoodPartner);
    }
}
