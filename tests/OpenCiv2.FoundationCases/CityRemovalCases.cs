using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static CityRemoval Removal(RevelationFixture f, List<CityTarget> removed, LossPresentation? presentation = null) =>
        new(f.World, f.Rules, f.Stacks, Loss(f, presentation ?? new()), removed.Add);

    private static void CityRemovalSupportedGarrisons()
    {
        var f = WorkingFixture(); List<CityTarget> removed = [];
        f.AddCity(1, 3, new(14, 6)); f.AddCity(2, 2, new(16, 8));
        f.World.Civilizations[1].CityCount = 1; f.World.Civilizations[1].TotalCitySize = 17;
        f.Rules.Units[4] = f.Rules.Units[0] with { Role = RuleRole.Defend };
        f.AddUnit(0, 1, 9, new(8, 6)); // Fixed Partisans, human, custom attack role: retained.
        f.AddUnit(1, 2, 4, new(14, 6)); // Lone AI defender rehomes even to a foreign city.
        f.AddUnit(2, 2, 4, new(16, 8)); f.AddUnit(3, 2, 4, new(16, 8));
        for (int i = 0; i < 4; ++i) f.Unit(i).Home = new(0);
        Removal(f, removed).Delete(0);
        Equal(true, f.Unit(0).Token.Occupied); Equal((CitySlot?)null, f.Unit(0).Home);
        Equal(true, f.Unit(1).Token.Occupied); Equal((CitySlot?)new CitySlot(1), f.Unit(1).Home);
        Equal(false, f.Unit(3).Token.Occupied); Equal(true, f.Unit(2).Token.Occupied);
        Equal((CitySlot?)new CitySlot(2), f.Unit(2).Home); // Restart sees one remaining defender after the descending deletion.
        Equal(true, f.World.Entities.Cities.Storage[2].Flags.DefenderLost);
        Equal((short)0, f.World.Civilizations[1].CityCount); Equal((short)17, f.World.Civilizations[1].TotalCitySize);
        Equal(false, f.World.Entities.Cities.Storage[0].Token.Occupied); Equal(3, f.World.Entities.Cities.End);
        Equal(new CitySlot(0), removed.Single().Slot); Equal(false, f.World.CityDeletionActive);
    }
    private static void CityRemovalRoutesWondersAndPayload()
    {
        var f = WorkingFixture(); List<CityTarget> removed = [];
        f.AddCity(1, 2, new(14, 6)); f.AddCity(2, 2, new(18, 6));
        ref var city = ref f.World.Entities.Cities.Storage[0]; city.Name = "Retained ruins"; city.FoodBox = -9;
        f.World.Civilizations[1].CityCount = short.MinValue;
        ref var other = ref f.World.Entities.Cities.Storage[1]; other.RouteCount = 3;
        for (int i = 0; i < 3; ++i) TradeRoutes.Write(f.World, new(1), i, i == 1 ? 2 : 0, i);
        f.World.Globals.WonderCity[0] = f.World.Globals.WonderCity[7] = 0; f.World.Globals.WonderCity[1] = 2;
        Removal(f, removed).Delete(0);
        Equal((byte)1, other.RouteCount); Equal((CitySlot?)new CitySlot(2), other.RoutePartners[0]); Equal((sbyte)1, other.RouteGoods[0]);
        Equal((CitySlot?)new CitySlot(0), other.RoutePartners[2]); Equal(true, other.Flags.TradeDirty); // Tail retained.
        Equal((short)-2, f.World.Globals.WonderCity[0]); Equal((short)-2, f.World.Globals.WonderCity[7]); Equal((short)2, f.World.Globals.WonderCity[1]);
        Equal("Retained ruins", city.Name); Equal((short)-9, city.FoodBox); Equal(short.MaxValue, f.World.Civilizations[1].CityCount);
        Removal(f, removed).Delete(1); Removal(f, removed).Delete(2);
        Equal(2, f.World.Entities.Cities.End); Equal((short)2, f.World.Globals.CityEnd); // One tail decrement, no hole trimming.
    }
    private static void CityRemovalSpatialVisibility()
    {
        var f = WorkingFixture(); List<CityTarget> removed = [];
        foreach (ref var tile in f.World.Map.Tiles) { tile.Fertility = 3; tile.Claimant = 1; }
        var center = new MapPoint(8, 6); var near = new MapPoint(10, 6); var ocean = new MapPoint(9, 5);
        f.World.Map.TileAt(center).Features |= MapFeatures.Pollution | MapFeatures.Road;
        f.World.Map.TileAt(ocean).Terrain = Terrain.Ocean;
        f.AddUnit(0, 1, 0, center); f.Unit(0).Visibility = CivilizationSet.Only(1);
        f.AddUnit(1, 2, 0, near);
        Removal(f, removed).Delete(0);
        Equal(false, f.World.Map.TileAt(center).Features.HasFlag(MapFeatures.CitySite));
        Equal(true, f.World.Map.TileAt(center).Features.HasFlag(MapFeatures.Pollution | MapFeatures.Road | MapFeatures.Unit));
        Equal((byte?)1, f.World.Map.TileAt(center).Owner); Equal((byte)11, f.World.Map.TileAt(center).Fertility);
        Equal((byte)0, f.World.Map.TileAt(center).Claimant); Equal((byte)3, f.World.Map.TileAt(ocean).Fertility);
        Equal((byte)1, f.World.Map.TileAt(ocean).Claimant); Equal(true, f.Unit(0).Visibility.Contains(1) && f.Unit(0).Visibility.Contains(2));
        f = WorkingFixture(); removed = [];
        f.AddCity(1, 2, new(10, 6)); f.AddCity(2, 3, new(8, 8));
        f.World.Map.TileAt(ocean).Terrain = Terrain.Ocean;
        Removal(f, removed).Delete(0);
        Equal((byte)3, f.World.Map.TileAt(center).Claimant); // Later city's radius wins, not nearest distance.
        Equal((byte)3, f.World.Map.TileAt(ocean).Claimant); // Reapplication includes ocean cells.
    }
    private static void CityRemovalRawHomesAndGuards()
    {
        var f = WorkingFixture(); List<CityTarget> removed = [];
        f.AddCity(255, 1, new(12, 6)); f.AddUnit(0, 1, 0, new(10, 6)); f.Unit(0).Home = null;
        f.World.Civilizations[1].CityCount = 2; f.World.Entities.Cities.Storage[0].RouteCount = 255;
        Removal(f, removed).Delete(255);
        Equal(false, f.Unit(0).Token.Occupied); Equal(255, f.World.Entities.Cities.End);
        Equal((byte)255, f.World.Entities.Cities.Storage[0].RouteCount); // Negative signed count has no route reads.
        f.World.CityDeletionActive = true; f.World.Globals.Multiplayer = GameMode.Tcp;
        Removal(f, removed).Delete(-1); Removal(f, removed).Delete(255); Equal(true, f.World.CityDeletionActive);
        Throws<InvalidOperationException>(() => Removal(f, removed).Delete(0));
        Equal(true, f.World.Entities.Cities.Storage[0].Token.Occupied);
    }
    private static void CityRemovalFailurePrefixes()
    {
        var f = WorkingFixture(); List<CityTarget> removed = [];
        f.World.Civilizations[1].CityCount = 1; f.AddUnit(0, 1, 0, new(10, 6)); f.Unit(0).Home = new(0);
        Throws<IOException>(() => Removal(f, removed, new() { FailRedraw = true }).Delete(0));
        Equal(false, f.Unit(0).Token.Occupied); Equal(true, f.World.Entities.Cities.Storage[0].Token.Occupied);
        Equal((short)0, f.World.Civilizations[1].CityCount); Equal(true, f.World.CityDeletionActive);
        Equal(0, removed.Count); Equal(true, f.World.Map.TileAt(new(8, 6)).Features.HasFlag(MapFeatures.CitySite));
        f = WorkingFixture(); removed = []; f.AddCity(1, 2, new(14, 6));
        f.World.Entities.Cities.Storage[1].RouteCount = 4; f.World.Globals.WonderCity[0] = 0;
        Throws<IndexOutOfRangeException>(() => Removal(f, removed).Delete(0));
        Equal(false, f.World.Entities.Cities.Storage[0].Token.Occupied); Equal((short)0, f.World.Globals.WonderCity[0]);
        Equal(true, f.World.Map.TileAt(new(8, 6)).Features.HasFlag(MapFeatures.CitySite));
    }
}
