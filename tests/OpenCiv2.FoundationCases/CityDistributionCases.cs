using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void CityDistributionRatesAndSpecialists()
    {
        var f = WorkingFixture(); var e = Economy(f); var w = f.World.Calculation;
        ref var civ = ref f.World.Civilizations[1]; ref var city = ref f.World.Entities.Cities.Storage[0];
        civ.ScienceRate = civ.TaxRate = 5;
        e.Distribute(new(0), 11); Equal(5, w.Science); Equal(6, w.Tax); Equal(0, w.Luxury);
        civ.ScienceRate = 6; civ.TaxRate = 3;
        e.Distribute(new(0), 11); Equal(7, w.Science); Equal(3, w.Tax); Equal(1, w.Luxury);
        civ.Government = Government.Fundamentalism; civ.TaxRate = 2;
        f.Rules.Cosmic = f.Rules.Cosmic with { FundamentalismScienceCapTenths = 2, FundamentalismScienceLossPercent = 50 };
        city.Specialists[0] = SpecialistKind.Entertainer; city.Specialists[1] = SpecialistKind.TaxCollector; city.Specialists[15] = SpecialistKind.Scientist;
        city.SpecialistCount = 0;
        f.World.Globals.HumanPlayers = default;
        e.Distribute(new(0), 20);
        Equal(6, w.Science); Equal(15, w.Tax); Equal(2, w.Luxury); // AI luxury conversion precedes specialist science/loss.
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
        e.Distribute(new(0), 20);
        Equal(4, w.Science); Equal(15, w.Tax); Equal(6, w.Luxury);
        city.Specialists = default; civ.ScienceRate = civ.TaxRate = 255;
        e.Distribute(new(0), -3);
        Equal(-3, w.Luxury); Equal(0, w.Science); Equal(0, w.Tax); // ClampAB preserves reversed upper bounds.
    }

    private static void CityDistributionFacilitiesAndWonders()
    {
        var f = WorkingFixture(); var e = Economy(f); var w = f.World.Calculation;
        ref var civ = ref f.World.Civilizations[1];
        civ.Government = Government.Fundamentalism; civ.ScienceRate = civ.TaxRate = 5;
        f.Rules.Cosmic = f.Rules.Cosmic with { FundamentalismScienceCapTenths = 10, FundamentalismScienceLossPercent = 0 };
        foreach (int a in new[] { AdvanceIds.Mysticism, AdvanceIds.CeremonialBurial, AdvanceIds.Electronics, AdvanceIds.Monotheism, AdvanceIds.Theology })
            civ.Advances = civ.Advances.With(a);
        foreach (int b in new[] { BuildingIds.Temple, BuildingIds.Colosseum, BuildingIds.Cathedral, BuildingIds.Marketplace, BuildingIds.Bank,
            BuildingIds.StockExchange, BuildingIds.Library, BuildingIds.University, BuildingIds.ResearchLab })
            BuildingQueries.SetBuilding(f.World, new(0), b, true);
        foreach (int wonder in new[] { WonderIds.Oracle, WonderIds.Bach, WonderIds.Newton, WonderIds.Copernicus, WonderIds.Seti })
            f.World.Globals.WonderCity[wonder] = 0;
        w.Processing = true;
        e.Distribute(new(0), 20);
        Equal(14, w.BuildingContent); Equal(60, w.Tax); Equal(80, w.Science);
        Equal((short)28, civ.GovernmentScore[(int)Government.Fundamentalism]); // -7 science +35 content economics.
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.ResearchLab, false);
        e.Distribute(new(0), 20); Equal(80, w.Science); // SETI substitutes once for Lab.
        f.Rules.WonderObsolescence[WonderIds.Newton] = AdvanceReference.None;
        f.World.Globals.WonderCity[WonderIds.Newton] = -1;
        e.Distribute(new(0), 20); Equal(50, w.Science); // (10 + floor(30/2))*2.
    }

    private static void CityCapitalizationAndScalarPublication()
    {
        var f = WorkingFixture(); var e = Economy(f); var w = f.World.Calculation;
        ref var civ = ref f.World.Civilizations[1]; ref var city = ref f.World.Entities.Cities.Storage[0];
        city.Production = -BuildingIds.Capitalization; city.Trade = 10; city.Tax = 17;
        civ.ScienceRate = civ.TaxRate = 5; w.Processing = true;
        e.Distribute(new(0), 20, 1);
        Equal((short)10, city.Science); Equal((short)22, city.Tax); Equal((short)20, city.Trade);
        Equal((short)-10, civ.GovernmentScore[(int)Government.Fundamentalism]); // Both passes contribute analysis.
        w.Shields = 9; w.ShieldSupport = 4;
        e.Distribute(new(0), 20); Equal((short)15, city.Tax);
        e.Distribute(new(0), 20, 2, -2); Equal((short)8, city.Tax);
        e.Distribute(new(0), 20, 3, 77); Equal((short)10, city.Tax);
        city.Production = 0; civ.ScienceRate = 0; civ.TaxRate = 10;
        e.Distribute(new(0), 32768); Equal((short)-32768, city.Tax); Equal((short)-32768, city.Trade);
        Equal(32768, w.Tax);
        f.World.Civilizations[1].Government = (Government)(-1);
        f.Rules.Terrain[(int)Terrain.Plains] = f.Rules.Terrain[(int)Terrain.Plains] with { Food = 4 };
        Equal(4, Workers(f).Yield(new(0), 0, CityYield.Food)); // Raw government255 has no low-government penalty.
        Equal(2, Workers(f).Yield(new(0), 0, CityYield.Trade));
    }

    private static void CityRecurringTradeSnapshots()
    {
        var f = WorkingFixture(); var e = Economy(f); var w = f.World.Calculation;
        ref var city = ref f.World.Entities.Cities.Storage[0];
        f.AddCity(1, 1, new(10, 6)); f.AddCity(2, 2, new(14, 6));
        f.World.Entities.Cities.Storage[1].BaseTrade = 28; f.World.Entities.Cities.Storage[1].Trade = 999;
        f.World.Entities.Cities.Storage[2].BaseTrade = 12;
        city.RouteCount = 2; city.RoutePartners[0] = new(1); city.RoutePartners[1] = new(2);
        city.RouteGoods[0] = 4; city.RouteGoods[1] = -1; city.Flags.TradeDirty = true;
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Superhighways, true);
        w.Trade = 20; w.Food = 258; w.Shields = 259; w.RouteTrade[2] = 77;
        f.World.Civilizations[1].ScienceRate = f.World.Civilizations[1].TaxRate = 5;
        e.TradeAndHappiness(new(0));
        Equal((short)20, city.BaseTrade); Equal(7, w.RouteTrade[0]); Equal(0, w.RouteTrade[1]); Equal(77, w.RouteTrade[2]);
        Equal(27, w.Trade); Equal((short)27, city.Trade); Equal((short)28, f.World.Entities.Cities.Storage[1].BaseTrade);
        Equal(false, city.Flags.TradeDirty); Equal(new MapPoint(14, 6), f.World.Paths.Goal); // Food route still ran its connection query.
        Equal((byte)2, city.TotalFood); Equal((byte)3, city.TotalShields);
        f.World.Entities.Cities.Storage[1].Owner = new(2); w.Trade = 20;
        city.RouteCount = 1;
        e.TradeAndHappiness(new(0)); Equal(15, w.RouteTrade[0]); Equal(35, w.Trade);
    }

    private static void CityMoodNormalizationExamples()
    {
        var f = WorkingFixture(); var h = new CityHappiness(f.World, f.Rules, f.Stacks); var w = f.World.Calculation; var m = f.World.Mood;
        ref var city = ref f.World.Entities.Cities.Storage[0];
        city.Size = 6; w.Unassigned = 2; m.Happy = 5; m.Unhappy = 4;
        h.Normalize(new(0), 0); Equal(2, m.Happy); Equal(1, m.Unhappy); Equal(0, w.ExcessUnhappy);
        city.Size = 3; w.Unassigned = 0; m.Happy = 256; m.Unhappy = -256;
        h.Normalize(new(0), 1); Equal(3, m.Happy); Equal(0, m.Unhappy); Equal((byte)3, w.HistoryHappy[1]);
        city.Size = 4; m.Happy = 0; m.Unhappy = w.ExcessUnhappy = 4;
        h.Normalize(new(0), 0); Equal(4, w.ExcessUnhappy);
        m.Happy = 2; h.Normalize(new(0), 1);
        Equal(2, m.Happy); Equal(2, m.Unhappy); Equal(2, w.ExcessUnhappy);
        city.Size = 3; w.Unassigned = -2; m.Happy = 0; m.Unhappy = 3; w.ExcessUnhappy = 10;
        h.Normalize(new(0), -1);
        Equal(5, m.Unhappy); Equal(4, w.ExcessUnhappy); // Last loop uses raw worker difference, not size clamp.
        city.Size = 3; w.Unassigned = 0; m.Happy = 256; m.Unhappy = -256; w.ExcessUnhappy = 0;
        Throws<IndexOutOfRangeException>(() => h.Normalize(new(0), 5));
        Equal(3, m.Happy); Equal(0, m.Unhappy); // Scalar normalization retained before excluded history write.
        city.Size = -2; m.Happy = 1; m.Unhappy = 0;
        h.Normalize(new(0), -1); Equal(-2, m.Happy); Equal(-2, m.Unhappy); // Reversed clamp bounds remain signed.
    }

    private static void CityMoodStagesAndMilitary()
    {
        var f = WorkingFixture(); var h = new CityHappiness(f.World, f.Rules, f.Stacks); var w = f.World.Calculation;
        ref var city = ref f.World.Entities.Cities.Storage[0]; ref var civ = ref f.World.Civilizations[1];
        city.Size = 6; civ.CityCount = 1;
        f.World.Globals.Difficulty = Difficulty.King;
        f.Rules.Cosmic = f.Rules.Cosmic with { UnhappinessCitySize = 7, UnhappinessEmpireSize = 14 };
        h.Calculate(new(0), 1); Equal((byte)3, w.HistoryUnhappy[0]);
        civ.CityCount = 24; h.Calculate(new(0), 1); Equal((byte)4, w.HistoryUnhappy[0]);
        f.World.Globals.Status.LargeMap = true; h.Calculate(new(0), 1); Equal((byte)3, w.HistoryUnhappy[0]);
        f.World.Globals.HumanPlayers = default; f.World.Globals.Difficulty = Difficulty.Deity;
        h.Calculate(new(0), 1); Equal((byte)3, w.HistoryUnhappy[0]); // AI formula ignores empire/difficulty.
        f.Rules.Cosmic = f.Rules.Cosmic with { UnhappinessCitySize = 20 };
        w.AggressiveUnits = 3; w.MartialLaw = 99;
        civ.Government = Government.Republic; h.Calculate(new(0), 1); Equal((byte)2, w.HistoryUnhappy[3]); Equal(99, w.MartialLaw);
        civ.Government = Government.Democracy; h.Calculate(new(0), 1); Equal((byte)6, w.HistoryUnhappy[3]);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.PoliceStation, true);
        h.Calculate(new(0), 1); Equal((byte)3, w.HistoryUnhappy[3]);
        civ.Government = Government.Republic; h.Calculate(new(0), 1); Equal((byte)0, w.HistoryUnhappy[3]);
        civ.Government = Government.Fundamentalism; h.Calculate(new(0), 1); Equal((byte)0, w.HistoryUnhappy[3]); Equal(99, w.MartialLaw);
        f.Rules.Cosmic = f.Rules.Cosmic with { UnhappinessCitySize = 7 };
        civ.Government = Government.Communism;
        f.Rules.Units[4] = new() { Attack = 255, Domain = RuleDomain.Air, Role = RuleRole.Trade };
        f.AddUnit(0, 2, 4, city.Position);
        h.Calculate(new(0), 1); Equal(2, w.MartialLaw); Equal((byte)1, w.HistoryUnhappy[3]); // Foreign/nonmilitary role still counts attack.
        f.World.Globals.WonderCity[WonderIds.Shakespeare] = 0;
        f.World.Globals.WonderCity[WonderIds.HangingGardens] = 0;
        f.World.Globals.WonderCity[WonderIds.CureForCancer] = 0;
        h.Calculate(new(0), 1); Equal(4, f.World.Mood.Happy); Equal(0, f.World.Mood.Unhappy);
        Equal(false, city.Flags.Disorder || city.Flags.Celebrating); // Calculation does not transition flags.
    }

    private static void CityFullCalculationAndCachedState()
    {
        var f = WorkingFixture(); var e = Economy(f); var w = f.World.Calculation;
        ref var city = ref f.World.Entities.Cities.Storage[0]; ref var civ = ref f.World.Civilizations[1];
        city.Size = 2; civ.Government = Government.Monarchy; civ.ScienceRate = civ.TaxRate = 5; civ.CityCount = 1;
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Palace, true);
        f.Rules.Cosmic = f.Rules.Cosmic with { FoodBoxRows = 10, ShieldBoxRows = 10, UnhappinessCitySize = 7, UnhappinessEmpireSize = 14 };
        f.AddUnit(0, 1, 0, city.Position); f.Unit(0).Home = new(0);
        w.Processing = true; w.Reallocating = true; w.JobPriority = 17;
        Equal(0, e.Calculate(new(0)));
        Equal((CitySlot?)new CitySlot(0), f.World.Mood.City);
        Equal(7, w.Food); Equal(3, w.Shields); Equal(4, w.Trade); Equal(1, w.ShieldSupport);
        Equal((short)2, city.Science); Equal((short)2, city.Tax); Equal((short)4, city.BaseTrade);
        Equal(true, f.Unit(0).Flags.CostsShield); Equal(1, w.HomeAttackUnits); Equal(1, w.MartialLaw);
        Equal(-17, w.Pollution); Equal(true, w.Processing && w.Reallocating); Equal(17, w.JobPriority);
        w.Food = 999; f.World.Mood.Happy = int.MaxValue; f.World.Mood.Unhappy = -1;
        Equal(int.MinValue, e.Calculate(new(60000), false)); Equal(999, w.Food);
        Equal(0, e.Calculate(null)); Equal((CitySlot?)new CitySlot(0), f.World.Mood.City);
        // A second full city rebuilds only declared fields, including raw overlap before allocation.
        f.AddCity(1, 2, new(10, 6));
        foreach (ref var tile in f.World.Map.Tiles) tile.RevealedTo = tile.RevealedTo.With(2);
        f.World.Civilizations[2].Government = Government.Republic;
        f.World.Civilizations[2].ScienceRate = 5; f.World.Civilizations[2].TaxRate = 5;
        e.Calculate(new(1));
        Equal((CitySlot?)new CitySlot(1), f.World.Mood.City);
        Equal(true, w.Availability.Any(s => s.HasFlag(TileAvailability.OtherCityWorked)));
        Equal(true, w.Processing && w.Reallocating);
    }

    private static void CityEconomyFailurePrefixes()
    {
        var f = WorkingFixture(); var e = Economy(f); var w = f.World.Calculation;
        ref var city = ref f.World.Entities.Cities.Storage[0];
        city.RouteCount = 1; city.RoutePartners[0] = null; city.Flags.TradeDirty = true;
        w.Trade = 20;
        Throws<InvalidOperationException>(() => e.TradeAndHappiness(new(0)));
        Equal((short)20, city.BaseTrade); Equal(false, city.Flags.TradeDirty); Equal(20, w.Trade);
        city.RouteCount = 4;
        for (int i = 0; i < 3; ++i) { city.RouteGoods[i] = -1; city.RoutePartners[i] = new(0); }
        w.Food = 8;
        Throws<IndexOutOfRangeException>(() => e.FoodRoutes(new(0)));
        Equal(3, w.FoodOut); Equal(8, w.Food); // Outgoing subtraction follows the completed scan.
        city.RouteCount = 0; f.World.Globals.Multiplayer = GameMode.Tcp;
        w.Food = 81;
        Throws<InvalidOperationException>(() => e.Calculate(new(0)));
        Equal((CitySlot?)new CitySlot(0), f.World.Mood.City); Equal(81, w.Food); // Cached-city selection precedes full pipeline.
    }

    private static void GeneratedCityCalculationIntegration()
    {
        foreach (bool flat in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(930));
            string? baseline = null;
            for (int run = 0; run < 2; ++run)
            {
                runtime.PrepareNewGame();
                InstallGeneratedStartingRules(runtime);
                var rules = runtime.Process.Rules.Definitions;
                InstallAccelerationUnits(rules);
                rules.Cosmic = rules.Cosmic with
                {
                    FoodPerCitizen = 2,
                    FoodBoxRows = 10,
                    ShieldBoxRows = 10,
                    SettlerFoodLowGovernment = 1,
                    SettlerFoodHighGovernment = 2,
                    MonarchyFreeSupport = 3,
                    UnhappinessCitySize = 7,
                    UnhappinessEmpireSize = 14
                };
                for (int c = 1; c < 8; ++c) runtime.Process.Rules.Leaders[c].Plural = new("tribes 0");
                var world = runtime.World;
                world.WorldSetup.Width = 80; world.WorldSetup.Height = 50;
                world.Globals.Rules.FlatWorld = flat; world.Globals.Difficulty = Difficulty.King;
                var starts = new StartingPositions(); var random = new GameplayRandom(47, "calculated-cities");
                WorldGeneration.Generate(runtime, ref starts, random, "world", new(1234));
                using (var initialize = new GeneratedGameInitialization(runtime, starts, random, "start"))
                {
                    for (int p = 0; p < 50_000 && !initialize.Completed; ++p) initialize.Poll(run == 0 ? 7 : 251);
                    Equal(true, initialize.Completed);
                }
                using (var accelerate = new GeneratedGameAcceleration(runtime, random, "accel", 1, new(NamingCatalog(["First", "Second"]))))
                {
                    while (!accelerate.Completed) accelerate.Poll();
                }
                var before = random.Capture();
                var economy = new CityEconomy(world, runtime.Process.Rules, new StackOperations(world, new(50_000_000, 64)));
                world.Calculation.Processing = world.Calculation.Reallocating = true;
                int calculated = 0;
                var results = new List<string>();
                for (int i = 0; i < world.Entities.Cities.End; ++i)
                {
                    ref var city = ref world.Entities.Cities.Storage[i];
                    if (!city.Token.Occupied) continue;
                    world.Calculation.JobFound = false; world.Calculation.JobPriority = 0;
                    int margin = economy.Calculate(new((ushort)i));
                    var w = world.Calculation;
                    Equal(unchecked(world.Mood.Happy - world.Mood.Unhappy), margin);
                    Equal(unchecked((byte)w.Food), city.TotalFood); Equal(unchecked((byte)w.Shields), city.TotalShields);
                    Equal(unchecked((short)(w.Trade - w.TradeCorruption)), city.Trade);
                    Equal((byte)(w.Unassigned & 63), city.SpecialistCount);
                    results.Add($"{i}:{city.Owner.Value}:{city.Size}:{city.TotalFood}:{city.TotalShields}:{city.Trade}:{city.Science}:{city.Tax}:{margin}:"
                        + $"{w.ShieldSupport}:{w.SupportedSettlers}:{w.CapitalCity}:{w.CapitalDistance}:{w.Pollution}:"
                        + string.Join(",", Enumerable.Range(0, 21).Where(city.WorkedTiles.Contains)));
                    ++calculated;
                }
                Equal(true, calculated >= 7); Equal(before, random.Capture()); // Recalculation consumes no random draws.
                string result = string.Join("|", results);
                if (run == 0) baseline = result;
                else Equal(baseline!, result);
                world.Calculation.Pollution = 999; world.Calculation.HistoryHappy[4] = 255; // Next fresh game discards it.
            }
        }
    }
}
