using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static CityCreation CityCreator(RevelationFixture f, Action? notice = null, NameCatalog? names = null)
    {
        f.Process.Rules.Leaders[0].Plural = new("tribes 0");
        return new(f.World, f.Process, names ?? NamingCatalog(["First", "Second"]),
            new UnitCreation(f.World, f.Process, f.Stacks, f), notice ?? (() => throw new InvalidOperationException("Unexpected city limit")));
    }

    private static void CityCreationInitialization()
    {
        var f = CommodityFixture();
        ref var city = ref f.World.Entities.Cities.Storage[0];
        city.Token = new(0);
        city.Flags = new() { Disorder = true, Coastal = true, OceanAccess = true };
        city.Size = 99; city.Founder = new(7); city.TurnsCaptured = 71;
        city.KnownTo = CivilizationSet.Only(7); city.RememberedSize[7] = 99;
        city.Specialists[0] = SpecialistKind.Scientist; city.SpecialistCount = 9;
        city.Name = "Old"; city.WorkedTiles = default(CityWorkSet).With(0).With(20);
        city.Improvements = default(ImprovementSet).With(BuildingIds.Factory);
        city.FoodBox = 100; city.ShieldBox = 200; city.BaseTrade = 300;
        city.Science = 11; city.Tax = 12; city.Trade = 13;
        city.TotalFood = 14; city.TotalShields = 15; city.Happy = 16; city.Unhappy = 17;
        city.RouteCount = 3; city.RouteGoods[0] = 12;
        city.RoutePartners[0] = new(7); city.RoutePartners[1] = new(0x1234); city.RoutePartners[2] = new(77);
        f.World.Civilizations[1].Tribe = 0;
        f.World.Civilizations[1].TotalCitySize = 77;
        f.World.Globals.Turn = 23;
        ref var tile = ref f.World.Map.TileAt(new(8, 6));
        tile.Features = MapFeatures.CitySite | MapFeatures.Fortification | MapFeatures.Road;
        tile.Owner = 6;
        f.Rules.Units[2] = new() { Role = RuleRole.Defend, Prerequisite = AdvanceReference.None, Defense = 2, CostRows = 2 };
        f.Rules.Units[3] = f.Rules.Units[2];
        f.Rules.Units[4] = f.Rules.Units[2] with { Defense = 1, CostRows = 1, Abilities = new() { AntiHorseDefense = true } };
        f.Rules.Units[5] = f.Rules.Units[2] with { Domain = RuleDomain.Sea, Defense = 100 }; // Cleared coast flags exclude it now.
        f.World.Civilizations[1].UnitsInProduction[4] = 255;
        Equal(new CitySlot(0), CityCreator(f).Create(new(1), new(8, 6))!.Value);
        Equal(1, f.World.Entities.Cities.End);
        Equal((short)1, f.World.Globals.CityEnd);
        Equal((uint)1, city.Token.Value);
        Equal((short)1, f.World.Civilizations[1].CityCount);
        Equal((short)23, f.World.Civilizations[1].LastCityTurn);
        Equal((short)8, f.World.Civilizations[1].CapitalX);
        Equal((short)77, f.World.Civilizations[1].TotalCitySize); // No population recount.
        Equal((sbyte)1, city.Size);
        Equal(new CivilizationSlot(1), city.Founder);
        Equal((byte)0, city.TurnsCaptured);
        Equal(0, city.KnownTo.Count);
        Equal((byte)0, city.RememberedSize[7]);
        Equal(SpecialistKind.Unused, city.Specialists[0]);
        Equal((byte)0, city.SpecialistCount);
        Equal(default(CityWorkSet), city.WorkedTiles); // Constructor does not assign workers.
        Equal("First", city.Name);
        Equal((sbyte)4, city.Production);
        Equal((byte)0, f.World.Civilizations[1].UnitsInProduction[4]);
        Equal(default(CityStatus), city.Flags); // Real commodity refresh cleared its dirty flag.
        Equal(default(ImprovementSet).With(BuildingIds.Palace), city.Improvements);
        Equal((byte?)6, tile.Owner); // Creation never rewrites occupant ownership.
        Equal(MapFeatures.CitySite | MapFeatures.Road, tile.Features);
        Equal((byte)1, tile.Claimant);
        Equal((short)0, city.FoodBox); Equal((short)0, city.ShieldBox); Equal((short)0, city.BaseTrade);
        Equal((short)0, city.Science); Equal((short)0, city.Tax); Equal((short)0, city.Trade);
        Equal((byte)0, city.TotalFood); Equal((byte)0, city.TotalShields); Equal((byte)0, city.Happy); Equal((byte)0, city.Unhappy);
        Equal((byte)0, city.RouteCount); Equal((sbyte)0, city.RouteGoods[0]);
        Equal(new CitySlot(0), city.RoutePartners[0]!.Value);
        Equal(new CitySlot(0x1200), city.RoutePartners[1]!.Value);
        Equal(new CitySlot(77), city.RoutePartners[2]!.Value);
        Equal(true, city.Supplies[0] >= 0 && city.Demands[0] >= 0);
    }

    private static void CityCreationProductionAndFailures()
    {
        var f = new RevelationFixture();
        var creator = CityCreator(f);
        f.Rules.Units[3] = new() { Role = RuleRole.Defend, Prerequisite = AdvanceReference.None, Defense = 1, CostRows = 255 };
        creator.Create(new(1), new(8, 6));
        Equal((sbyte)0, f.World.Entities.Cities.Storage[0].Production); // All negative scores leave Settlers.
        f.Rules.Units[3] = f.Rules.Units[3] with { Defense = 0, CostRows = 1 };
        f.Rules.Units[61] = f.Rules.Units[3];
        creator.Create(new(1), new(12, 6));
        Equal((sbyte)61, f.World.Entities.Cities.Storage[1].Production); // Later zero ties replace the initial choice.
        f.Rules.Units[3] = f.Rules.Units[3] with { Defense = 255, CostRows = 255 };
        creator.Create(new(1), new(16, 6));
        Equal((sbyte)3, f.World.Entities.Cities.Storage[2].Production); // Both operands are signed.
        f.Rules.Units[3] = f.Rules.Units[3] with { CostRows = 0 };
        Throws<DivideByZeroException>(() => creator.Create(new(1), new(20, 6)));
        Equal(4, f.World.Entities.Cities.End);
        Equal((short)4, f.World.Civilizations[1].CityCount);
        Equal(true, f.World.Entities.Cities.Storage[3].Token.Occupied);
        Equal(true, f.World.Map.TileAt(new(20, 6)).City);
        Equal((byte)1, f.World.Civilizations[1].UnitsInProduction[0]); // Failed scan did not count production.
        var failing = new NameCatalog(NamingCatalog(["First"]).Layers, (_, _) => throw new InvalidDataException("Name provider failed"));
        f.Rules.Units[3] = f.Rules.Units[3] with { CostRows = 1 };
        Throws<InvalidDataException>(() => CityCreator(f, names: failing).Create(new(1), new(24, 6)));
        Equal((short)5, f.World.Civilizations[1].CityCount);
        Equal((byte)2, f.World.Civilizations[1].UnitsInProduction[61]); // Naming failure retains the completed production scan.
        f.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => creator.Create(new(1), new(28, 6)));
        Equal(5, f.World.Entities.Cities.End);
    }

    private static void CityCreationLimitsAndWrap()
    {
        var f = new RevelationFixture();
        f.World.Entities.Cities.SetEnd(255);
        for (int i = 0; i < 255; ++i) f.World.Entities.Cities.Storage[i].Token = new((uint)i + 1);
        int notices = 0;
        var creator = CityCreator(f, () => ++notices);
        Equal((CitySlot?)null, creator.Create(new(1), new(8, 6)));
        Equal(1, notices);
        Equal((CitySlot?)null, creator.Create(new(2), new(8, 6)));
        Equal(1, notices);
        f.World.Entities.Cities.Storage[7].Token = new(0);
        f.Process.Tokens.NextCity = uint.MaxValue;
        f.World.Civilizations[1].CityCount = short.MaxValue;
        Equal(new CitySlot(7), creator.Create(new(1), new(8, 6))!.Value);
        Equal(short.MinValue, f.World.Civilizations[1].CityCount);
        Equal(uint.MaxValue, f.World.Entities.Cities.Storage[7].Token.Value);
        Equal((uint)0, f.Process.Tokens.NextCity);
        f.World.Entities.Cities.Storage[8].Token = new(0);
        Equal(new CitySlot(8), creator.Create(new(1), new(12, 6))!.Value);
        Equal((uint)0, f.World.Entities.Cities.Storage[8].Token.Value);
        Equal((uint)1, f.Process.Tokens.NextCity);
        Equal(255, f.World.Entities.Cities.End);
    }

    private static void CityCreationAiFirstCity()
    {
        foreach (var (turn, apollo, size, units, facilities) in new[]
        {
            (40, false, 1, 0, false), (41, false, 2, 1, true), (220, false, 10, 1, true), (100, true, 1, 1, false)
        })
        {
            var f = new RevelationFixture();
            f.World.Globals.Turn = (short)turn;
            if (apollo) f.World.Globals.WonderCity[WonderIds.Apollo] = 250; // Raw location, no validity/expiry query.
            f.Rules.Units[3] = new() { Role = RuleRole.Defend, Prerequisite = AdvanceReference.None, Defense = 2, CostRows = 2, HitPoints = 10 };
            f.Rules.Terrain[(int)Terrain.Grassland] = new() { Work = new(new(TerrainResult.ImproveInPlace), new()) };
            f.Rules.Terrain[(int)Terrain.Ocean] = f.Rules.Terrain[(int)Terrain.Grassland];
            f.World.Map.TileAt(new(9, 5)).Terrain = Terrain.Ocean; // No extra non-Ocean irrigation restriction.
            f.World.Map.TileAt(new(10, 6)).RevealedTo = CivilizationSet.Only(1); // Human exploration suppresses this irrigation.
            CityCreator(f).Create(new(2), new(8, 6));
            ref var city = ref f.World.Entities.Cities.Storage[0];
            Equal((sbyte)size, city.Size);
            Equal(units, f.World.Entities.Units.End);
            Equal(facilities, city.Improvements.Contains(BuildingIds.Temple));
            Equal(facilities, city.Improvements.Contains(BuildingIds.Marketplace));
            Equal(facilities, city.Improvements.Contains(BuildingIds.Library));
            Equal(true, city.Improvements.Contains(BuildingIds.Palace));
            Equal(true, f.World.Map.TileAt(new(9, 5)).Features.HasFlag(MapFeatures.Irrigation));
            Equal(false, f.World.Map.TileAt(new(10, 6)).Features.HasFlag(MapFeatures.Irrigation));
            Equal(false, f.World.Map.TileAt(new(10, 4)).Features.HasFlag(MapFeatures.Irrigation)); // B[8] is outside irrigation range.
            Equal(true, f.World.Map.TileAt(new(10, 4)).RevealedTo.Contains(2));
            Equal(apollo, city.KnownTo.Contains(7));
            if (apollo)
            {
                Equal((byte)1, city.RememberedSize[7]);
                Equal(true, f.World.Map.TileAt(new(8, 6)).RevealedTo.Contains(0));
                Equal(MapFeatures.CitySite, f.World.Map.Remembered(7, new(8, 6)));
            }
            if (units != 0)
            {
                Equal(new CitySlot(0), f.Unit(0).Home!.Value);
                Equal(new CivilizationSlot(2), f.Unit(0).Owner);
                Equal((byte)3, f.Unit(0).Type.Value);
            }
        }
    }

    private static void CityCreationCoastAndArea()
    {
        foreach (int oceans in new[] { 1, 2 })
        {
            var f = new RevelationFixture();
            f.World.Map.Regions[63] = new(0, 0, 100, default);
            for (int i = 0; i < oceans; ++i)
            {
                ref var ocean = ref f.World.Map.TileAt(i == 0 ? new(9, 5) : new(10, 6));
                ocean.Terrain = Terrain.Ocean;
                ocean.Region = 63;
            }
            f.World.Map.TileAt(new(7, 5)).Terrain = Terrain.Mountains;
            CityCreator(f).Create(new(1), new(8, 6));
            var flags = f.World.Entities.Cities.Storage[0].Flags;
            Equal(true, flags.Coastal);
            Equal(oceans == 2, flags.OceanAccess); // A prior Ocean visit enables the second region63 visit.
            Equal(true, flags.HydroPossible);
        }
        var area = new RevelationFixture();
        foreach (ref var tile in area.World.Map.Tiles)
        {
            tile.Fertility = 15; tile.Claimant = 7; tile.Owner = 6;
        }
        area.AddCity(0, 2, new(10, 10));
        CityArea.Refresh(area.World, new(0));
        Equal((byte)7, area.World.Map.TileAt(new(15, 9)).Fertility); // B[29], outer ring.
        Equal((byte)7, area.World.Map.TileAt(new(15, 9)).Claimant);
        Equal((byte)2, area.World.Map.TileAt(new(13, 9)).Claimant); // Inner radius.
        Equal((byte?)6, area.World.Map.TileAt(new(13, 9)).Owner);
        Equal((byte)15, area.World.Map.TileAt(new(16, 10)).Fertility); // Beyond all45 entries.
        area.AddCity(1, 3, new(12, 10));
        CityArea.Refresh(area.World, new(1));
        Equal((byte)3, area.World.Map.TileAt(new(13, 9)).Claimant); // Later overlap wins.
    }

    private static void CityRegionAndPortQueries()
    {
        var f = new RevelationFixture();
        MapPoint origin = new(10, 10);
        f.World.Map.TileAt(origin).Region = 4;
        f.AddCity(0, 1, new(8, 10));
        f.AddCity(1, 2, new(12, 10));
        f.World.Map.TileAt(new(8, 10)).Region = 5;
        f.World.Map.TileAt(new(12, 10)).Region = 6;
        Equal(new CitySlot(1), CityQueries.Nearest(f.World, origin, requestedRegion: 4).Slot!.Value);
        Equal((CitySlot?)null, CityQueries.Nearest(f.World, origin, requestedRegion: 6).Slot);
        Equal(9999, CityQueries.Nearest(f.World, origin, requestedRegion: 6).Distance);
        f.World.Map.TileAt(origin).Terrain = Terrain.Ocean;
        Equal(new CitySlot(1), CityQueries.Nearest(f.World, origin, requestedRegion: 27).Slot!.Value);
        Equal((CitySlot?)null, CityQueries.Nearest(f.World, origin, requestedRegion: -2).Slot);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.PortFacility, true);
        Equal(new CitySlot(0), CityQueries.Nearest(f.World, origin, requestedRegion: -2).Slot!.Value);
        Equal(new CitySlot(0), CityQueries.Nearest(f.World, origin, owner: 257, requestedRegion: -2).Slot!.Value);
        Equal((CitySlot?)null, CityQueries.Nearest(f.World, origin, owner: 2, alternateOwner: 257, requestedRegion: -2).Slot);
        Equal(new CitySlot(0), CityQueries.Nearest(f.World, origin, owner: 2, alternateOwner: 1, requestedRegion: -2).Slot!.Value);
    }

    private static void CityCoastalQueries()
    {
        var f = new RevelationFixture();
        f.AddCity(0, 1, new(10, 10));
        f.AddCity(1, 2, new(24, 10));
        Equal((0, 0, 8), CityQueries.AdjacentSea(f.World, new(0)));
        foreach (var (point, region) in new (MapPoint, byte)[]
        {
            (new(11, 9), 2), // D0 ocean causes D1 and D2 to be skipped.
            (new(12, 10), 3),
            (new(10, 12), 63), // D3 ocean skips D4; D5 is ordinary land.
            (new(9, 9), 4), // D6 ocean leaves traversal index9.
            (new(25, 9), 3)
        })
        {
            f.World.Map.TileAt(point).Terrain = Terrain.Ocean;
            f.World.Map.TileAt(point).Region = region;
        }
        f.World.Map.Regions[2] = new(0, 0, 100, default);
        f.World.Map.Regions[3] = new(0, 0, 999, default);
        f.World.Map.Regions[4] = new(0, 0, 20, default);
        f.World.Map.Regions[63] = new(0, 0, 500, default);
        Equal((4, 100, 9), CityQueries.AdjacentSea(f.World, new(0)));
        Equal(true, CityQueries.TouchesBody(f.World, new(0), 3)); // This predicate scans every direction.
        Equal(false, CityQueries.TouchesBody(f.World, new(0), 63));
        Equal(false, CityQueries.TouchesBody(f.World, new(0), -1));
        Equal(true, CityQueries.ShareOcean(f.World, new(0), new(1)));
        f.World.Map.TileAt(new(25, 9)).Region = 63;
        Equal(false, CityQueries.ShareOcean(f.World, new(0), new(1)));
        f.World.Entities.Cities.Storage[0].Token = new(0);
        Equal(true, CityQueries.TouchesBody(f.World, new(0), 2)); // No lifetime gate on backed spatial queries.
    }
}
