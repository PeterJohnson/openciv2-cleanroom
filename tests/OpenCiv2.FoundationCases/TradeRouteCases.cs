using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static RevelationFixture RouteFixture()
    {
        var f = CommodityFixture();
        for (int i = 1; i <= 4; ++i) f.AddCity(i, 2, new(8 + 2 * i, 6));
        f.World.Globals.Turn = 7;
        return f;
    }
    private static void RouteRemovalAndWriting()
    {
        var f = RouteFixture(); ref var city = ref f.World.Entities.Cities.Storage[0];
        city.RouteCount = 3;
        for (int i = 0; i < 3; ++i) TradeRoutes.Write(f.World, new(0), i, i + 1, i + 4);
        TradeRoutes.Remove(f.World, new(0), 1);
        Equal((byte)2, city.RouteCount); Equal((CitySlot?)new CitySlot(3), city.RoutePartners[1]);
        Equal((sbyte)6, city.RouteGoods[1]); Equal(city.RoutePartners[1], city.RoutePartners[2]);
        Equal(true, city.Flags.TradeDirty);
        city.RouteCount = 0; city.Flags.TradeDirty = false;
        TradeRoutes.Remove(f.World, new(0), 0);
        Equal((byte)255, city.RouteCount); Equal((sbyte)4, city.RouteGoods[0]); Equal(true, city.Flags.TradeDirty);
        TradeRoutes.Write(f.World, new(0), 0, 65537, 100);
        Equal((CitySlot?)new CitySlot(1), city.RoutePartners[0]); Equal((sbyte)15, city.RouteGoods[0]);
        TradeRoutes.Write(f.World, new(0), 1, -1, -22);
        Equal((CitySlot?)null, city.RoutePartners[1]); Equal((sbyte)-1, city.RouteGoods[1]);
        TradeRoutes.Write(f.World, new(0), 1, -2, 0);
        Equal((CitySlot?)new CitySlot(65534), city.RoutePartners[1]); // Signed16 payload retained; using it as a city needs backing.
    }
    private static void RouteAppendDuplicatesAndRefresh()
    {
        var f = RouteFixture(); ref var city = ref f.World.Entities.Cities.Storage[0];
        f.World.Entities.Cities.Storage[1].Supplies[0] = 83;
        TradeRoutes.Add(f.World, f.Rules, new(0), 1, 15);
        Equal((byte)1, city.RouteCount); Equal(false, city.Flags.TradeDirty);
        Equal((sbyte)83, f.World.Entities.Cities.Storage[1].Supplies[0]); // Destination's own refresh gate skipped it.
        city.Flags.TradeDirty = true;
        TradeRoutes.Add(f.World, f.Rules, new(0), 1, 15);
        Equal((byte)1, city.RouteCount); Equal(true, city.Flags.TradeDirty); // Exact match returns before refresh.
        TradeRoutes.Add(f.World, f.Rules, new(0), 1, 16);
        Equal((byte)2, city.RouteCount); Equal((sbyte)15, city.RouteGoods[1]);
        f = RouteFixture();
        TradeRoutes.Add(f.World, f.Rules, new(0), 1, -1); TradeRoutes.Add(f.World, f.Rules, new(0), 2, -1);
        Equal((byte)2, f.World.Entities.Cities.Storage[0].RouteCount);
    }
    private static void RouteReplacementValuationAndRepair()
    {
        var f = RouteFixture(); ref var city = ref f.World.Entities.Cities.Storage[0];
        city.RouteCount = 3; city.BaseTrade = 300;
        for (int i = 0; i < 3; ++i)
        {
            TradeRoutes.Write(f.World, new(0), i, i + 1, i);
            f.World.Entities.Cities.Storage[i + 1].BaseTrade = 20;
            f.World.Map.TileAt(f.World.Entities.Cities.Storage[i + 1].Position).Region = (byte)(i + 2); // No route connection.
        }
        f.World.Entities.Cities.Storage[4].BaseTrade = 20;
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Superhighways, true);
        TradeRoutes.Add(f.World, f.Rules, new(0), 4, 5);
        Equal((CitySlot?)new CitySlot(4), city.RoutePartners[0]); Equal((CitySlot?)new CitySlot(2), city.RoutePartners[1]);
        // Adjust old routes with airport and owner; compare the new destination's unadjusted trade.
        for (int i = 0; i < 3; ++i) TradeRoutes.Write(f.World, new(0), i, i + 1, i);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Airport, true);
        for (int i = 1; i <= 3; ++i) BuildingQueries.SetBuilding(f.World, new((ushort)i), BuildingIds.Airport, true);
        f.World.Entities.Cities.Storage[2].Owner = new(1); // 20*1.5/2 = 15 wins over 30.
        f.World.Entities.Cities.Storage[4].BaseTrade = 15;
        TradeRoutes.Add(f.World, f.Rules, new(0), 4, 7);
        Equal((CitySlot?)new CitySlot(4), city.RoutePartners[1]);
        city.RouteGoods[0] = -1; var before = city;
        TradeRoutes.Add(f.World, f.Rules, new(0), 3, -1); Equal(before, city);
        for (int i = 0; i < 3; ++i)
        {
            TradeRoutes.Write(f.World, new(0), i, i + 1, i);
            f.World.Entities.Cities.Storage[i + 1].BaseTrade = 30000;
        }
        f.World.Entities.Cities.Storage[4].BaseTrade = 20000; before = city;
        TradeRoutes.Add(f.World, f.Rules, new(0), 4, 9); Equal(before, city); // No score beats sentinel; repaired no replacement.
    }
    private static void RouteMutationFailurePrefixes()
    {
        var f = RouteFixture(); ref var city = ref f.World.Entities.Cities.Storage[0];
        city.RouteCount = 255;
        Throws<IndexOutOfRangeException>(() => TradeRoutes.Add(f.World, f.Rules, new(0), 1, 0));
        Equal((byte)0, city.RouteCount); Equal(false, city.Flags.TradeDirty);
        city.RouteCount = 4;
        TradeRoutes.Write(f.World, new(0), 1, 2, 5); TradeRoutes.Write(f.World, new(0), 2, 3, 6); city.Flags.TradeDirty = false;
        Throws<IndexOutOfRangeException>(() => TradeRoutes.Remove(f.World, new(0), 0));
        Equal((byte)4, city.RouteCount); Equal((CitySlot?)new CitySlot(2), city.RoutePartners[0]);
        Equal((CitySlot?)new CitySlot(3), city.RoutePartners[1]); Equal(false, city.Flags.TradeDirty);
        city.RouteCount = 0;
        Throws<ArgumentOutOfRangeException>(() => TradeRoutes.Add(f.World, f.Rules, new(0), 60000, 1));
        Equal((byte)1, city.RouteCount); Equal((CitySlot?)new CitySlot(60000), city.RoutePartners[0]);
    }
}
