using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual) =>
        Equal(string.Join(",", expected), string.Join(",", actual));

    private static RevelationFixture CommodityFixture()
    {
        var f = new RevelationFixture();
        f.World.Map.Allocate(new(20, 12));
        foreach (ref var tile in f.World.Map.Tiles)
        {
            tile.Terrain = Terrain.Plains;
            tile.ResourceSuppressed = true;
            tile.Region = 1;
        }
        f.World.Map.Regions[1] = new(100, 0, 0, default);
        f.World.Civilizations[1].Tribe = 1;
        f.AddCity(0, 1, new(8, 6), 3);
        return f;
    }

    private static void CommodityPublishedVector()
    {
        // Complete raw-score example published in C07, not a captured implementation snapshot.
        var f = CommodityFixture();
        var scores = Commodities.Score(f.World, f.Rules, new(0));
        Sequence(new[] { 0, 0, 0, 15, 0, 0, 0, -42, 4, 1, 0, 0, 11, 2, -1, -1 }, scores.Supply);
        Sequence(new[] { 0, 48, 46, 0, 24, 0, 0, 15, 10, 15, 24, 5, 0, 24, -1, -1 }, scores.Demand);
        Equal(true, Commodities.Refresh(f.World, f.Rules, new(0)));
        ref var city = ref f.World.Entities.Cities.Storage[0];
        Sequence(new sbyte[] { 3, 6, 13 }, ((ReadOnlySpan<sbyte>)city.Supplies).ToArray());
        Sequence(new sbyte[] { 1, 2, 13 }, ((ReadOnlySpan<sbyte>)city.Demands).ToArray());
        Equal(false, city.Flags.TradeDirty);
    }

    private static void CommoditySurveyAndRoadRepair()
    {
        var f = CommodityFixture();
        // W01's pattern at (8,6) with seed12 is resource kind2. Only terrain gets the extra weight.
        f.World.Map.PatternSeed = 12;
        ref var center = ref f.World.Map.TileAt(new(8, 6));
        center.Terrain = Terrain.Mountains;
        center.ResourceSuppressed = false;
        center.River = true;
        center.Features |= MapFeatures.Road;
        f.World.Map.TileAt(new(9, 5)).Features |= MapFeatures.Road;
        f.World.Map.TileAt(new(10, 6)).Features |= MapFeatures.Irrigation;
        Equal((byte)2, f.World.Map.SpecialResource(new(8, 6)));
        var scores = Commodities.Score(f.World, f.Rules, new(0));
        Equal(20, scores.Supply[(int)Commodity.Copper]);
        Equal(8, scores.Supply[(int)Commodity.Silver]);
        Equal(12, scores.Supply[(int)Commodity.Hides]);
        Equal(1, scores.Demand[(int)Commodity.Copper]); // (one river + two roads + 1)/2, then small-city halving.
        Equal(16, scores.Demand[(int)Commodity.Dye]); // (60-1)>>2 + two roads.
        center.ResourceSuppressed = true;
        scores = Commodities.Score(f.World, f.Rules, new(0));
        Equal(5, scores.Supply[(int)Commodity.Copper]);
        Equal(2, scores.Supply[(int)Commodity.Silver]);
        Equal(1, scores.Demand[(int)Commodity.Copper]);
        center.Features &= ~MapFeatures.Road;
        f.World.Map.TileAt(new(9, 5)).Features &= ~MapFeatures.Road;
        scores = Commodities.Score(f.World, f.Rules, new(0));
        Equal(0, scores.Demand[(int)Commodity.Copper]); // A fresh zero road base on this call.
        Equal(14, scores.Demand[(int)Commodity.Dye]);
    }

    private static void CommodityTechnologyAndSignedScores()
    {
        var f = CommodityFixture();
        foreach (ref var tile in f.World.Map.Tiles) tile.Terrain = Terrain.Glacier;
        ref var city = ref f.World.Entities.Cities.Storage[0];
        ref var civ = ref f.World.Civilizations[1];
        city.Size = 2;
        civ.NumAdvances = 20;
        civ.Advances = default(AdvanceSet).With(AdvanceIds.Industrialization).With(AdvanceIds.MassProduction);
        var scores = Commodities.Score(f.World, f.Rules, new(0));
        Equal(26, scores.Demand[(int)Commodity.Cloth]); // Captured before the Hides overrides.
        Equal(1, scores.Demand[(int)Commodity.Hides]);
        Equal(21, scores.Supply[(int)Commodity.Oil]); // Glacier merges into Tundra: 21*8/8.
        Equal(0, scores.Demand[(int)Commodity.Oil]);
        civ.Advances = civ.Advances.With(AdvanceIds.Combustion);
        Equal(21, Commodities.Score(f.World, f.Rules, new(0)).Supply[(int)Commodity.Oil]);
        f.World.Globals.AdvanceKnownBy[AdvanceIds.Combustion] = CivilizationSet.Only(7);
        Equal(168, Commodities.Score(f.World, f.Rules, new(0)).Supply[(int)Commodity.Oil]);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Factory, true);
        Equal(9, Commodities.Score(f.World, f.Rules, new(0)).Demand[(int)Commodity.Oil]);
        city.Size = -127;
        scores = Commodities.Score(f.World, f.Rules, new(0));
        Equal(-103, scores.Demand[(int)Commodity.Oil]); // Negative factory product divides toward zero.
        Equal(-10, scores.Demand[(int)Commodity.Salt]);
        civ.Advances = civ.Advances.With(AdvanceIds.NuclearFission);
        Equal(50, Commodities.Score(f.World, f.Rules, new(0)).Demand[(int)Commodity.Uranium]);
    }

    private static void CommodityFacilitiesAndTribes()
    {
        var f = CommodityFixture();
        ref var city = ref f.World.Entities.Cities.Storage[0];
        ref var civ = ref f.World.Civilizations[1];
        civ.NumAdvances = 40;
        city.Size = 10;
        city.Improvements = default(ImprovementSet).With(BuildingIds.Cathedral).With(BuildingIds.Bank);
        civ.Tribe = 17;
        civ.Advances = default(AdvanceSet).With(AdvanceIds.Economics).With(AdvanceIds.Computers);
        var scores = Commodities.Score(f.World, f.Rules, new(0));
        Equal(80, scores.Demand[(int)Commodity.Silver]); // Gold selected; original Silver remains.
        Equal(90, scores.Demand[(int)Commodity.Gold]); // 80*3/2*3/2 >>1 >>1, then Spanish doubling.
        Equal(0, scores.Demand[(int)Commodity.Gems]);
        city.Improvements = city.Improvements.With(BuildingIds.Cathedral, false);
        f.World.Globals.WonderCity[WonderIds.Michelangelo] = 0;
        Equal(90, Commodities.Score(f.World, f.Rules, new(0)).Demand[(int)Commodity.Gold]);
        f.Rules.WonderObsolescence[WonderIds.Michelangelo] = (AdvanceReference)AdvanceIds.Economics;
        Equal(60, Commodities.Score(f.World, f.Rules, new(0)).Demand[(int)Commodity.Gold]);
        civ.Tribe = 9;
        int wine = Commodities.Score(f.World, f.Rules, new(0)).Supply[(int)Commodity.Wine];
        civ.Tribe = 1;
        Equal(2 * Commodities.Score(f.World, f.Rules, new(0)).Supply[(int)Commodity.Wine], wine);
        int silver = Commodities.Score(f.World, f.Rules, new(0)).Supply[(int)Commodity.Silver];
        int silk = Commodities.Score(f.World, f.Rules, new(0)).Supply[(int)Commodity.Silk];
        foreach (short tribe in new short[] { 10, 11 })
        {
            civ.Tribe = tribe;
            scores = Commodities.Score(f.World, f.Rules, new(0));
            Equal(2 * silk, scores.Supply[(int)Commodity.Silk]);
            Equal(silver, scores.Supply[(int)Commodity.Silver]);
        }
    }

    private static void CommodityGatesAndRanking()
    {
        var f = CommodityFixture();
        ref var city = ref f.World.Entities.Cities.Storage[0];
        city.Supplies[0] = -12;
        f.World.Globals.Turn = 1;
        Equal(false, Commodities.Refresh(f.World, f.Rules, new(0)));
        Equal((sbyte)-12, city.Supplies[0]);
        f.World.Globals.RevealMap = 1;
        Equal(true, Commodities.Refresh(f.World, f.Rules, new(0)));
        f.World.Globals.RevealMap = 0;
        city.Flags.TradeDirty = true;
        Equal(true, Commodities.Refresh(f.World, f.Rules, new(0)));
        f.World.Globals.Turn = -16;
        Equal(true, Commodities.Refresh(f.World, f.Rules, new(0)));
        Commodities.Rank(ref city, new int[16], new int[16]);
        Sequence(new sbyte[] { 15, 14, 13 }, ((ReadOnlySpan<sbyte>)city.Supplies).ToArray());
        Sequence(new sbyte[] { 15, 14, 13 }, ((ReadOnlySpan<sbyte>)city.Demands).ToArray());
        var negative = Enumerable.Repeat(-1, 16).ToArray();
        negative[0] = negative[1] = 0;
        Throws<InvalidOperationException>(() => Commodities.Rank(ref f.World.Entities.Cities.Storage[0], negative, new int[16]));
        Equal((sbyte)1, city.Supplies[0]);
        Equal((sbyte)0, city.Supplies[1]); // Safe earlier publications retained on unsupported third read.
    }

    private static void CommodityExceptionalAndSuppression()
    {
        var f = CommodityFixture();
        ref var city = ref f.World.Entities.Cities.Storage[0];
        city.Supplies[0] = 12; city.Supplies[1] = 6; city.Supplies[2] = 0;
        city.Demands[0] = 13; city.Demands[1] = 2; city.Demands[2] = 14;
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Industrialization).With(AdvanceIds.NuclearFission);
        Commodities.Exceptional(f.World, new(0), 0);
        Equal((sbyte)14, city.Supplies[1]); // Collision can insert Oil despite no normal Oil supply.
        Equal((sbyte)2, city.Demands[1]); // Existing advanced demand suppresses the fallback.
        city.Demands[2] = 1;
        Commodities.Exceptional(f.World, new(0), 0);
        Equal((sbyte)15, city.Demands[1]); // Collision sentinel -2 selects Uranium.

        f.AddCity(1, 7, new(14, 6));
        ref var other = ref f.World.Entities.Cities.Storage[1];
        other.Token = new(0); // Backed route records need not be live.
        other.RouteCount = 2;
        other.RoutePartners[0] = other.RoutePartners[1] = new(0);
        other.RouteGoods[0] = 14; other.RouteGoods[1] = 13;
        city.Supplies[0] = 0; city.Supplies[1] = 4; city.Supplies[2] = 6;
        city.Demands[0] = 13; city.Demands[1] = 14; city.Demands[2] = 15;
        city.RouteCount = 2;
        city.RouteGoods[0] = 0; city.RoutePartners[0] = new(1);
        city.RouteGoods[1] = -1; city.RoutePartners[1] = null; // Food skips the partner entirely.
        f.AddUnit(0, 7, 3, new(8, 6)); // No owner match is required for supply suppression.
        f.Unit(0).Home = new(0); f.Unit(0).Counter = 4;
        f.AddUnit(1, 1, 3, new(8, 6));
        f.Unit(1).Home = null; f.Unit(1).Counter = 6;
        Commodities.Suppress(f.World, f.Rules, new(0));
        Sequence(new sbyte[] { 0, -4, 6 }, ((ReadOnlySpan<sbyte>)city.Supplies).ToArray());
        Sequence(new sbyte[] { -13, 14, 15 }, ((ReadOnlySpan<sbyte>)city.Demands).ToArray());
    }
}
