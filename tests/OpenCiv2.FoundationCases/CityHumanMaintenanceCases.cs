using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static CityHumanMaintenance HumanMaintenance(CityPollutionFixture f, List<ImprovementLostNotice> notices, bool fail = false)
    {
        return new(f.World, f.Pollution, new(f.World, f.Data.Rules, n =>
        { notices.Add(n); if (fail) throw new InvalidOperationException("Maintenance notice unavailable"); }));
    }
    private static void MaintenanceTemple(CityPollutionFixture f, int owner = 1)
    {
        f.World.Entities.Cities.Storage[0].Owner = new((byte)owner);
        f.World.Civilizations[owner].Government = Government.Monarchy;
        f.World.Calculation.ShieldRows = 10;
        f.Data.Rules.Improvements[BuildingIds.Temple] = new() { CostRows = 2, Upkeep = 1 };
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Temple, true);
    }
    private static void CityHumanMaintenanceAdmission()
    {
        foreach (bool capturedHuman in new[] { false, true })
        {
            var f = new CityPollutionFixture(capturedHuman ? [CityPollutionChoice("risk", 256)] : []); MaintenanceTemple(f, 2);
            f.World.Globals.HumanPlayers = CivilizationSet.Only(capturedHuman ? 1 : 2); f.World.Globals.CivsAlive = default;
            List<ImprovementLostNotice> losses = []; var upkeep = HumanMaintenance(f, losses);
            Equal(0, upkeep.Run(new(0), 1, "pollution").Count);
            Equal(capturedHuman ? 1 : 0, losses.Count); Equal(capturedHuman ? 20 : 0, f.World.Civilizations[2].Gold);
            Equal(!capturedHuman, BuildingQueries.HasBuilding(f.World, new(0), BuildingIds.Temple));
            f.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
        }
        var remote = new CityPollutionFixture([]); remote.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => HumanMaintenance(remote, []).Run(new(0), 1, "pollution"));
    }
    private static void CityHumanMaintenancePollutionBeforePayment()
    {
        var f = new CityPollutionFixture([CityPollutionChoice("risk", 256), CityPollutionChoice("tile", 20)]); MaintenanceTemple(f);
        f.World.Calculation.Pollution = 1; var point = new MapPoint(11, 9); f.World.Map.TileAt(point).Terrain = Terrain.Grassland;
        var buildings = new BuildingMaintenance(f.World, f.Data.Rules, _ => Equal(true, f.World.Map.TileAt(point).Features.HasFlag(MapFeatures.Pollution)));
        Equal(0, new CityHumanMaintenance(f.World, f.Pollution, buildings).Run(new(0), 1, "pollution").Count);
        Equal((short)1, f.World.Globals.PollutedTiles); Equal(20, f.World.Civilizations[1].Gold);
        Equal(false, BuildingQueries.HasBuilding(f.World, new(0), BuildingIds.Temple)); f.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
    }
    private static void CityHumanMaintenanceMeltdownBeforePayment()
    {
        var f = new CityPollutionFixture([CityPollutionChoice("risk", 256)]); MaintenanceTemple(f); f.Reactor();
        f.World.Globals.Difficulty = Difficulty.Deity;
        f.Data.Rules.Improvements[BuildingIds.NuclearPlant] = new() { CostRows = 100, Upkeep = 255 };
        List<ImprovementLostNotice> losses = [];
        Equal(0, HumanMaintenance(f, losses).Run(new(0), 1, "pollution").Count);
        Equal((sbyte)5, f.World.Entities.Cities.Storage[0].Size);
        Equal(false, BuildingQueries.HasBuilding(f.World, new(0), BuildingIds.NuclearPlant));
        Equal(BuildingIds.Temple, losses.Single().Building); Equal(20, f.World.Civilizations[1].Gold);
        f.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
    }
    private static void CityHumanMaintenanceCosmeticDiagnostics()
    {
        var f = new CityPollutionFixture([CityPollutionChoice("risk", 256), CityPollutionChoice("tile", 20)]); MaintenanceTemple(f);
        f.World.Calculation.Pollution = 1; f.World.Map.TileAt(new(11, 9)).Terrain = Terrain.Grassland;
        f.World.Map.TileAt(new(11, 9)).RevealedTo = CivilizationSet.Only(1); f.FailPresentation = true;
        // Two forced losses show that a failed notice preserves the credit and the remaining scan.
        f.Data.Rules.Improvements[BuildingIds.Temple] = new() { CostRows = 0, Upkeep = 1 };
        f.Data.Rules.Improvements[BuildingIds.Library] = new() { CostRows = 2, Upkeep = 1 };
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Library, true);
        List<ImprovementLostNotice> losses = [];
        Equal(4, HumanMaintenance(f, losses, true).Run(new(0), 1, "pollution").Count);
        Equal((short)1, f.World.Globals.PollutedTiles); Equal(20, f.World.Civilizations[1].Gold); Equal(2, losses.Count);
        Equal(false, BuildingQueries.HasBuilding(f.World, new(0), BuildingIds.Library)); f.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
    }
    private static void CityHumanMaintenanceRequiredFailures()
    {
        var f = new CityPollutionFixture([]); MaintenanceTemple(f); f.World.Civilizations[1].Gold = 17;
        Throws<ChoiceException>(() => HumanMaintenance(f, []).Run(new(0), 1, "pollution"));
        Equal(17, f.World.Civilizations[1].Gold); Equal(true, BuildingQueries.HasBuilding(f.World, new(0), BuildingIds.Temple));
        var later = new CityPollutionFixture([CityPollutionChoice("risk", 256), CityPollutionChoice("tile", 20)]);
        later.World.Civilizations[1].Government = Government.Monarchy; later.World.Calculation.Pollution = 1;
        later.World.Map.TileAt(new(11, 9)).Terrain = Terrain.Grassland;
        BuildingQueries.SetBuilding(later.World, new(0), BuildingIds.Barracks, true);
        later.Data.Rules.Advances[AdvanceIds.MobileWarfare] = new() { Prerequisites = new([(AdvanceReference)AdvanceIds.MobileWarfare, AdvanceReference.None]) };
        Throws<InvalidOperationException>(() => HumanMaintenance(later, []).Run(new(0), 1, "pollution"));
        Equal((short)1, later.World.Globals.PollutedTiles); Equal(true, BuildingQueries.HasBuilding(later.World, new(0), BuildingIds.Barracks));
        later.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
    }
    private static void LocalCityPollutionFromActualEconomy()
    {
        foreach (bool console in new[] { false, true })
        {
            using var game = new LocalGame(); FoundCouncil(game);
            var source = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            var city = source.Cities.Single(); var worked = default(CityWorkSet).With(20);
            foreach (var tile in game.Player.ObserveLocalPlay()!.City!.Tiles.Where(t => t.Index != 20).OrderByDescending(t => t.Shields).Take(8))
                worked = worked.With(tile.Index);
            city = city with { Size = 8, FoodBox = 0, WorkedTiles = StateConversion.EncodeCityWork(worked), SpecialistKinds = 0 };
            var civs = source.Civilizations.ToArray(); var civ = StateConversion.ToLive(civs[1]);
            foreach (int advance in new[] { AdvanceIds.Industrialization, AdvanceIds.Automobile, AdvanceIds.MassProduction, AdvanceIds.Plastics })
                civ.Advances = civ.Advances.With(advance);
            civ.TotalCitySize = 8; civ.NumAdvances = 4; civ.Gold = 1000;
            civs[1] = StateConversion.PreserveOpaque(StateConversion.ToStored(civ), civs[1]);
            Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(source with { Cities = new([city]), Civilizations = new(civs) })));
            CommandUpdate? update = null; StoredSave saved = source;
            for (int turn = 2; turn <= 100 && saved.Globals.PollutedTiles == 0; ++turn)
            {
                update = EndLocalTurn(game, console, turn); saved = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            }
            Equal((short)1, saved.Globals.PollutedTiles); Equal((sbyte)0, saved.Globals.WarmingCounter);
            Equal(true, update!.Committed.Any(e => e.Name == "city.pollution"));
            Equal(LocalLoadResult.Loaded, game.LoadClassic(game.SaveClassic()));
            Equal((short)1, ClassicFiles.DecodeStoredSave(game.SaveClassic()).Globals.PollutedTiles);
        }
    }
}
