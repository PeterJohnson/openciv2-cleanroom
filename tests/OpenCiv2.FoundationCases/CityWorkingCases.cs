using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static RevelationFixture WorkingFixture()
    {
        var f = CommodityFixture();
        foreach (ref var tile in f.World.Map.Tiles) tile.RevealedTo = CivilizationSet.Only(1);
        f.World.Civilizations[1].Government = Government.Monarchy;
        f.Rules.Cosmic = f.Rules.Cosmic with { FoodPerCitizen = 2 };
        for (int i = 0; i < 11; ++i)
            f.Rules.Terrain[i] = new() { Food = 2, Shields = 1, Trade = 1, Work = new(new(TerrainResult.ImproveInPlace, 1), new(TerrainResult.ImproveInPlace, 2)) };
        return f;
    }
    private static CityWorking Workers(RevelationFixture f) => new(f.World, f.Rules, f.Stacks);

    private static void CityYieldModifiers()
    {
        var f = WorkingFixture(); var worker = Workers(f);
        ref var tile = ref f.World.Map.TileAt(new(9, 5));
        ref var civ = ref f.World.Civilizations[1];
        civ.Government = Government.Despotism;
        tile.Features = MapFeatures.Irrigation;
        Equal(2, worker.Yield(new(0), 0, CityYield.Food)); // 2+1-1.
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Supermarket, true);
        Equal(2, worker.Yield(new(0), 0, CityYield.Food)); // Irrigation without farmland.
        tile.Features |= MapFeatures.Mine;
        Equal(3, worker.Yield(new(0), 0, CityYield.Food)); // 3+(3>>1)-1.
        tile.Features |= MapFeatures.Pollution;
        Equal(2, worker.Yield(new(0), 0, CityYield.Food));
        Equal(1, f.World.Calculation.JobPriority);
        Equal(new MapPoint(9, 5), f.World.Calculation.JobPosition);
        tile.Features = MapFeatures.Road | MapFeatures.Railroad | MapFeatures.Mine;
        Equal(3, worker.Yield(new(0), 0, CityYield.Shields)); // (1+2)*1.5, then penalty.
        f.World.Globals.WonderCity[WonderIds.KingRichard] = 0;
        Equal(5, worker.Yield(new(0), 0, CityYield.Shields)); // (1+2+1)*1.5 -1.
        civ.Government = Government.Republic;
        f.World.Globals.WonderCity[WonderIds.Colossus] = 0;
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Superhighways, true);
        tile.River = true;
        Equal(7, worker.Yield(new(0), 0, CityYield.Trade)); // 1+river+road+Colossus+Republic, then 5/2.
        civ.Advances = civ.Advances.With(0);
        f.Rules.WonderObsolescence[WonderIds.Colossus] = (AdvanceReference)0;
        Equal(6, worker.Yield(new(0), 0, CityYield.Trade)); // Actual active-wonder query.
        tile.Terrain = Terrain.Ocean;
        tile.Features = MapFeatures.None; tile.River = false;
        f.World.Globals.Multiplayer = GameMode.Hotseat;
        f.World.Multiplayer.Saved.DoubleProduction = true;
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Harbor, true);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.OffshorePlatform, true);
        Equal(5, worker.Yield(new(0), 0, CityYield.Food)); // Base only doubles; Harbor once, no Supermarket.
        Equal(4, worker.Yield(new(0), 0, CityYield.Shields)); // 2 + Offshore + Richard.
        f.Rules.Terrain[(int)Terrain.Ocean] = f.Rules.Terrain[(int)Terrain.Ocean] with { Trade = 0 };
        f.Rules.WonderObsolescence[WonderIds.Colossus] = AdvanceReference.Never;
        Equal(2, worker.Yield(new(0), 0, CityYield.Trade)); // Colossus turns zero ocean trade positive, Republic adds one.
        tile.Terrain = Terrain.Forest;
        f.Rules.Terrain[(int)Terrain.Forest] = f.Rules.Terrain[(int)Terrain.Forest] with { Trade = 0 };
        Equal(0, worker.Yield(new(0), 0, CityYield.Trade)); // Land zero has no road/river to enable Colossus.
    }

    private static void CityYieldResourcesAndSignedRules()
    {
        var f = WorkingFixture(); var worker = Workers(f);
        var work = f.World.Calculation;
        ref var center = ref f.World.Map.TileAt(new(8, 6));
        center.Terrain = Terrain.Mountains; center.ResourceSuppressed = false;
        f.World.Map.PatternSeed = 12;
        Equal((byte)2, f.World.Map.SpecialResource(new(8, 6)));
        f.Rules.Terrain[(int)Terrain.Mountains + 22] = new() { Food = 6, Shields = 7, Trade = 8 };
        Equal(7, worker.Yield(new(0), 20, CityYield.Food)); // Resource yield + base-terrain irrigation.
        Equal(7, worker.Yield(new(0), 20, CityYield.Shields)); // Center irrigation bonus nonzero suppresses auto-mining.
        f.Rules.Terrain[(int)Terrain.Mountains] = f.Rules.Terrain[(int)Terrain.Mountains] with { Work = new(new(TerrainResult.NoResult, 0), new(TerrainResult.NoResult, 255)) };
        Equal(6, worker.Yield(new(0), 20, CityYield.Shields)); // Signed mining bonus -1.
        f.World.Entities.Cities.Storage[0].Position = new(8, 4);
        center = ref f.World.Map.TileAt(new(8, 4));
        center.Features = MapFeatures.CitySite;
        center.ResourceSuppressed = true; center.Terrain = Terrain.Grassland;
        Equal(false, Geometry.GrasslandShield(new(8, 4)));
        Equal(1, worker.Yield(new(0), 20, CityYield.Shields)); // Grassland suppression, then center minimum.
        center.Terrain = Terrain.Mountains;
        f.Rules.Terrain[(int)Terrain.Mountains] = f.Rules.Terrain[(int)Terrain.Mountains] with { Shields = 255 };
        Equal(0, worker.Yield(new(0), 20, CityYield.Shields)); // Negative -2 is not changed to center minimum.
        ref var civ = ref f.World.Civilizations[1];
        civ.Government = Government.Republic;
        civ.GovernmentScore[(int)Government.Despotism] = short.MinValue;
        f.Rules.Terrain[(int)Terrain.Mountains] = f.Rules.Terrain[(int)Terrain.Mountains] with { Food = 4 };
        work.Processing = true;
        Equal(4, worker.Yield(new(0), 20, CityYield.Food));
        Equal(short.MaxValue, civ.GovernmentScore[(int)Government.Despotism]);
        center.Terrain = Terrain.Ocean;
        f.Rules.Terrain[(int)Terrain.Ocean] = new() { Trade = 254 };
        civ.GovernmentScore[(int)Government.Republic] = short.MaxValue;
        civ.GovernmentScore[(int)Government.Democracy] = short.MaxValue;
        Equal(0, worker.Yield(new(0), 20, CityYield.Trade)); // Negative nonzero still scores and receives government +1.
        Equal(short.MinValue, civ.GovernmentScore[(int)Government.Republic]);
        Equal(short.MinValue, civ.GovernmentScore[(int)Government.Democracy]);
        Equal(0, work.Totals[0]); Equal(0, work.TileYield[0]);
    }

    private static void CityAvailabilityAndPollutionVisits()
    {
        var f = WorkingFixture(); var worker = Workers(f); var work = f.World.Calculation;
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        work.Processing = true;
        f.World.Globals.WarmingPollution = short.MaxValue;
        f.World.Map.TileAt(new(9, 5)).Features |= MapFeatures.Pollution;
        f.World.Map.TileAt(new(10, 6)).Features |= MapFeatures.Pollution;
        f.World.Map.TileAt(new(9, 7)).Features |= MapFeatures.Pollution;
        f.World.Map.TileAt(new(9, 7)).RevealedTo = default;
        f.AddUnit(0, 2, 0, new(9, 5)); // Human armed foreign unit.
        f.AddUnit(1, 2, 0, new(10, 6));
        f.World.Map.TileAt(new(10, 6)).Terrain = Terrain.Ocean;
        f.AddUnit(2, 2, 0, new(8, 8));
        f.AddUnit(3, 1, 0, new(8, 8)); // Own front stops before hostile member.
        f.Unit(3).Next = new(2); f.Unit(2).Previous = new(3); // Explicit imported mixed-owner chain.
        f.AddUnit(4, 2, 0, new(7, 7));
        f.Rules.Units[4] = f.Rules.Units[0] with { Attack = 0 };
        f.AddUnit(5, 3, 4, new(7, 7)); // Foreign unarmed front permits traversal.
        f.Unit(5).Next = new(4); f.Unit(4).Previous = new(5);
        f.World.Map.TileAt(new(6, 6)).Features = MapFeatures.CitySite | MapFeatures.Fortification; // Airbase is not a city.
        f.World.Map.TileAt(new(6, 6)).Owner = 2;
        worker.RefreshAvailability(new(0));
        Equal((short)-32767, f.World.Globals.WarmingPollution);
        Equal(TileAvailability.HostileUnit | TileAvailability.HumanBlocker, work.Availability[0]);
        Equal(TileAvailability.None, work.Availability[1]);
        Equal(TileAvailability.Unusable, work.Availability[2]);
        Equal(TileAvailability.None, work.Availability[3]);
        Equal(TileAvailability.HostileUnit | TileAvailability.HumanBlocker, work.Availability[4]);
        Equal(TileAvailability.None, work.Availability[5]);
        Equal(TileAvailability.CityPresent, work.Availability[20]);
        for (int i = 21; i < 25; ++i) Equal(TileAvailability.Unusable, work.Availability[i]);
        f.World.Civilizations[1].Treaties[3].Alliance = true;
        worker.RefreshAvailability(new(0));
        Equal(TileAvailability.None, work.Availability[4]);
        Equal((short)-32765, f.World.Globals.WarmingPollution); // Repeated visits, not deduplication.
        work.Processing = false;
        worker.RefreshAvailability(new(0));
        Equal((short)-32765, f.World.Globals.WarmingPollution);
        f.World.Entities.Cities.Storage[0].WorkedTiles = default(CityWorkSet).With(0).With(19).With(20);
        work.Availability[19] = (TileAvailability)128; // Any status releases a worker.
        Equal(true, worker.ReleaseBlocked(new(0)));
        Equal(default(CityWorkSet).With(20), f.World.Entities.Cities.Storage[0].WorkedTiles);
    }

    private static void CityTerrainSuggestionsAndWater()
    {
        var f = WorkingFixture(); var worker = Workers(f); var work = f.World.Calculation;
        var p = new MapPoint(9, 5);
        f.World.Map.TileAt(new(11, 5)).River = true; // Non-Q direction is not irrigation water.
        Equal(false, TerrainSources.Irrigation(f.World, p));
        f.World.Map.TileAt(new(10, 4)).River = true;
        Equal(true, TerrainSources.Irrigation(f.World, p));
        work.ApplyYield = work.Processing = true;
        work.JobPriority = 8;
        f.Rules.Terrain[(int)Terrain.Plains] = f.Rules.Terrain[(int)Terrain.Plains] with
        { Work = new(new(TerrainResult.ImproveInPlace, 1, 0, 255), new(TerrainResult.ImproveInPlace, 2, 0, 1)) };
        Equal(2, worker.Yield(new(0), 0, CityYield.Food));
        Equal(true, work.JobFound); Equal(9, work.JobPriority); Equal(p, work.JobPosition);
        Equal((byte?)1, f.World.Map.TileAt(p).Owner); Equal((byte?)1, f.World.Map.TileAt(p).Claimant);
        work.JobFound = false;
        f.World.Map.TileAt(p).River = true;
        worker.Yield(new(0), 0, CityYield.Trade);
        Equal(false, work.JobFound); // River road suggestion requires Bridge Building.
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.BridgeBuilding);
        worker.Yield(new(0), 0, CityYield.Trade);
        Equal(true, work.JobFound); Equal(9, work.JobPriority); // Road does not increment priority.
        f.World.Map.TileAt(p).Features |= MapFeatures.Pollution;
        work.ApplyYield = work.Processing = false;
        work.JobPriority = int.MaxValue;
        worker.Accumulate(new(0), 0, false);
        Equal(int.MinValue + 2, work.JobPriority); // Every kind overwrites pollution job despite preview mode.
        Equal(false, work.ApplyYield); Sequence(new[] { 0, 0, 0 }, work.Totals);
        work.JobPriority = 0;
        worker.Accumulate(new(0), 0, true);
        Sequence(new[] { 1, 1, 1 }, work.Totals); // ceil(2,1,2)/2.
        Equal(3, work.JobPriority);
        f.World.Globals.Rules.FlatWorld = false;
        f.World.Map.TileAt(new(19, 5)).Features = MapFeatures.Irrigation;
        Equal(true, TerrainSources.Irrigation(f.World, new(0, 4)));
    }

    private static void CityWorkerTiesAndRetention()
    {
        var f = WorkingFixture(); var worker = Workers(f); var work = f.World.Calculation;
        ref var city = ref f.World.Entities.Cities.Storage[0];
        city.Size = 2;
        worker.RefreshAvailability(new(0));
        worker.Assign(new(0));
        Equal(default(CityWorkSet).With(20).With(0).With(1), city.WorkedTiles);
        Sequence(new[] { 7, 3, 4 }, work.Totals);
        Equal(0, work.Unassigned); Equal((byte)0, city.SpecialistCount);
        city.WorkedTiles = default(CityWorkSet).With(6).With(8).With(9); // Exactly size+1 without center remains.
        worker.Assign(new(0));
        Equal(default(CityWorkSet).With(6).With(8).With(9), city.WorkedTiles);
        city.WorkedTiles = city.WorkedTiles.With(12); // Excess workers reconsider the whole set.
        worker.Assign(new(0));
        Equal(true, work.WorkersOverflow);
        Equal(default(CityWorkSet).With(20).With(0).With(1), city.WorkedTiles);
        city.Size = 4; city.WorkedTiles = default; city.SpecialistCount = 2;
        city.Specialists[0] = SpecialistKind.Scientist; city.Specialists[1] = SpecialistKind.TaxCollector;
        worker.Assign(new(0));
        Equal(2, work.Unassigned); Equal((byte)2, city.SpecialistCount);
        Equal(SpecialistKind.Scientist, city.Specialists[0]); // Allocation does not normalize kinds.
        city.Size = 2; city.SpecialistCount = 3; city.WorkedTiles = default;
        worker.Assign(new(0));
        Equal(default(CityWorkSet), city.WorkedTiles); // Kept shortcut even with a missing center.
        Sequence(new[] { 0, 0, 0 }, work.Totals);
        Equal((short)-1, city.FoodBox);
    }

    private static void CityWorkerGreedyStages()
    {
        var f = WorkingFixture(); var worker = Workers(f); var work = f.World.Calculation;
        ref var city = ref f.World.Entities.Cities.Storage[0];
        city.Size = 1;
        f.World.Map.TileAt(city.Position).Terrain = Terrain.Ocean;
        f.Rules.Terrain[(int)Terrain.Ocean] = new();
        for (int i = 2; i < 20; ++i) work.Availability[i] = TileAvailability.Unusable;
        f.World.Map.TileAt(new(9, 5)).Terrain = Terrain.Forest;
        f.World.Map.TileAt(new(9, 5)).Features = MapFeatures.Pollution;
        f.Rules.Terrain[(int)Terrain.Forest] = new() { Food = 3, Trade = 1, Work = new() };
        f.World.Map.TileAt(new(10, 6)).Terrain = Terrain.Mountains;
        f.Rules.Terrain[(int)Terrain.Mountains] = new() { Shields = 1, Work = new() };
        worker.Assign(new(0));
        Equal(default(CityWorkSet).With(20).With(1), city.WorkedTiles); // Last worker must produce a shield.
        Equal(2, work.JobPriority); // Food+Shields preview, skipped Trade, no applied polluted tile.
        Sequence(new[] { 0, 1, 0 }, work.Totals);
        // With no positive candidate the two allocation phases both finish, leaving specialists.
        f.World.Map.TileAt(new(9, 5)).Features = MapFeatures.None;
        f.Rules.Terrain[(int)Terrain.Forest] = new() { Work = new() };
        f.Rules.Terrain[(int)Terrain.Mountains] = new() { Work = new() };
        city.WorkedTiles = default; city.SpecialistCount = 0;
        worker.Assign(new(0));
        Equal(default(CityWorkSet).With(20), city.WorkedTiles);
        Equal(1, work.Unassigned);
        // A selected polluted candidate is evaluated again, so its pollution suggestion is not cached.
        city.Size = 3; city.WorkedTiles = default; city.SpecialistCount = 0;
        work.JobPriority = 0;
        f.World.Map.TileAt(new(9, 5)).Features = MapFeatures.Pollution;
        f.Rules.Terrain[(int)Terrain.Forest] = new() { Food = 4, Shields = 2, Trade = 2, Work = new() };
        worker.Assign(new(0));
        Equal(true, city.WorkedTiles.Contains(0)); Equal(6, work.JobPriority);
        Sequence(new[] { 2, 1, 1 }, work.Totals);
        Equal(2, work.Unassigned);
    }

    private static void CityWorkerAiCadenceAndSupport()
    {
        var f = WorkingFixture(); var worker = Workers(f); var work = f.World.Calculation;
        ref var city = ref f.World.Entities.Cities.Storage[0];
        f.World.Globals.HumanPlayers = default;
        city.Size = 3;
        city.WorkedTiles = default(CityWorkSet).With(20).With(8).With(9).With(10);
        worker.RefreshAvailability(new(0));
        f.World.Globals.Turn = 1;
        worker.Assign(new(0));
        Equal(true, city.WorkedTiles.Contains(10));
        f.World.Globals.Turn = 4;
        worker.Assign(new(0));
        Equal(default(CityWorkSet).With(20).With(0).With(1).With(2), city.WorkedTiles);
        city.WorkedTiles = default(CityWorkSet).With(20).With(8).With(9).With(10);
        work.Availability[19] = TileAvailability.HumanBlocker; // Not necessarily a worked blocked square.
        worker.Assign(new(0));
        Equal(true, city.WorkedTiles.Contains(10));
        work.Availability[19] = TileAvailability.None;
        city.SpecialistCount = 1; city.WorkedTiles = default(CityWorkSet).With(20).With(0).With(1);
        work.Reallocating = true;
        worker.Assign(new(0));
        Equal((byte)0, city.SpecialistCount); Equal(false, city.Flags.SpecialistHeld);
        city.SpecialistCount = 1; city.Flags.MoodOnEdge = true;
        city.WorkedTiles = default(CityWorkSet).With(20).With(0).With(1);
        worker.Assign(new(0));
        Equal((byte)1, city.SpecialistCount); Equal(true, city.Flags.SpecialistHeld);
        city.Flags.Disorder = true; city.Size = 6; city.SpecialistCount = 0; city.WorkedTiles = default;
        worker.Assign(new(0));
        Equal((byte)1, city.SpecialistCount); Equal(true, city.Flags.SpecialistHeld);
        // Declared C05 outputs are inputs here; the full support parent is not supplied by this fixture.
        work.SupportedSettlers = 3; work.SettlerFood = 2;
        Equal(18, worker.FoodRequirement(new(0)));
        city.FoodBox = 0;
        worker.Assign(new(0));
        Equal((short)-1, city.FoodBox);
        work.SupportedSettlers = 0; city.Flags.Disorder = false; city.SpecialistCount = 0;
        worker.Assign(new(0));
        Equal((short)0, city.FoodBox);
    }

    private static void CityWorkerSpecialistDomains()
    {
        var f = WorkingFixture(); var worker = Workers(f); var work = f.World.Calculation;
        ref var city = ref f.World.Entities.Cities.Storage[0];
        city.SpecialistCount = 63;
        worker.AdjustSpecialists(new(0), 1); Equal((byte)0, city.SpecialistCount);
        worker.AdjustSpecialists(new(0), int.MinValue); Equal((byte)0, city.SpecialistCount);
        worker.AdjustSpecialists(new(0), 66); Equal((byte)2, city.SpecialistCount);
        worker.SetSpecialist(new(0), 0, SpecialistKind.Scientist);
        worker.SetSpecialist(new(0), 15, SpecialistKind.Scientist);
        Equal(2, worker.CountSpecialists(new(0), SpecialistKind.Scientist)); // Counts inactive storage too.
        Equal(SpecialistKind.Entertainer, worker.Specialist(new(60000), 16)); // No city backing read.
        worker.SetSpecialist(new(60000), 16, SpecialistKind.Scientist);
        work.Unassigned = 64;
        worker.FillSpecialists(new(0));
        Equal(64, work.SpecialistCount); Equal(14, worker.CountSpecialists(new(0), SpecialistKind.Entertainer));
        work.Unassigned = 1;
        worker.FillSpecialists(new(0));
        Equal(SpecialistKind.Scientist, city.Specialists[0]); Equal(SpecialistKind.Unused, city.Specialists[15]);
        Throws<ArgumentOutOfRangeException>(() => worker.Specialist(new(0), -1));
        work.Unassigned = -1;
        Throws<ArgumentOutOfRangeException>(() => worker.FillSpecialists(new(0)));
        Equal(-1, work.SpecialistCount); // Prefix precedes the unsupported negative kind index.
        city.Size = 64; city.WorkedTiles = default; city.SpecialistCount = 0;
        for (int i = 0; i < 20; ++i) work.Availability[i] = TileAvailability.Unusable;
        worker.Assign(new(0));
        Equal(64, work.Unassigned); Equal((byte)0, city.SpecialistCount); // Full remainder, six-bit publication.
        worker.FillSpecialists(new(0)); Equal(64, work.SpecialistCount);
        city.Size = -2;
        worker.Assign(new(0));
        Equal(-1, work.Unassigned); Equal((byte)63, city.SpecialistCount);
    }

    private static void CityWorkingFailureAndIsolation()
    {
        var f = WorkingFixture(); var worker = Workers(f); var work = f.World.Calculation;
        work.Totals[0] = 17; work.ApplyYield = true; work.JobPriority = 9;
        f.World.Entities.Cities.Storage[0].Position = new(0, 0);
        Equal(0, worker.Yield(new(0), 6, CityYield.Food));
        Equal(17, work.Totals[0]); Equal(9, work.JobPriority);
        f.World.Entities.Cities.Storage[0].Position = new(8, 6);
        f.Rules.Terrain[(int)Terrain.Plains] = new() { Food = 2 }; // Missing required work rule is not zero bonus.
        Equal(2, worker.Yield(new(0), 0, CityYield.Food)); // Processing gate skips the unused job rules.
        work.Processing = true;
        Throws<InvalidOperationException>(() => worker.Accumulate(new(0), 0, true));
        Equal(true, work.ApplyYield); Equal(17, work.Totals[0]);
        Equal((byte?)1, f.World.Map.TileAt(new(9, 5)).Claimant); // Real map prefix is retained.
        f.World.Globals.Multiplayer = GameMode.Tcp;
        work.Availability[0] = TileAvailability.HumanBlocker;
        Throws<InvalidOperationException>(() => worker.RefreshAvailability(new(0)));
        Equal(TileAvailability.HumanBlocker, work.Availability[0]);
        var fresh = new GameState(f.Process);
        Equal(false, ReferenceEquals(work, fresh.Calculation));
        Equal(0, fresh.Calculation.Totals[0]); Equal(false, fresh.Calculation.ApplyYield);
        Equal(0, fresh.Calculation.JobPriority);
    }
}
