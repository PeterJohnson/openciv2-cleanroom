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
    private static RevelationFixture AccelerationFixture()
    {
        var f = new RevelationFixture();
        f.World.Globals.Year = -4000;
        f.World.Map.Tiles.Fill(new() { Terrain = Terrain.Plains, Region = 1, ResourceSuppressed = true, Fertility = 15 });
        f.World.Map.Regions[1] = new(480, 64, 0, default);
        foreach (int type in new[] { 0, 2, 3, 4, 5, 6, 15, 16, 17 })
        {
            f.Rules.Units[type] = new()
            {
                Role = type == 0 ? RuleRole.Settle : type == 3 ? RuleRole.Defend : RuleRole.Attack,
                Prerequisite = AdvanceReference.None,
                CostRows = 2,
                HitPoints = 10,
                Movement = 3,
                Attack = 1,
                Defense = 2
            };
        }
        for (int c = 1; c < 8; ++c) f.World.Civilizations[c].Researching = -1;
        f.AddUnit(0, 1, 0, new(20, 12));
        return f;
    }

    private static GameplayRandom AccelerationChoices(params (int Civ, int Gold, int? Pair)[] choices)
    {
        var supplied = new List<Choice>();
        foreach (var choice in choices)
        {
            supplied.Add(Choice.Create(new("F05.accelerated.treasury", "accel", choice.Civ),
                new SamplingLaw.PrimaryModulo(50), ChoiceCode.Primary(choice.Gold)));
            if (choice.Pair is { } pair) supplied.Add(Choice.Create(new("F05.accelerated.defenders", "accel", choice.Civ),
                new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(pair)));
        }
        return new(new ControlledChoices(new(supplied)), "acceleration");
    }

    private static AcceleratedStarts Acceleration(RevelationFixture f, GameplayRandom random, int era = 0, Action? notice = null) =>
        new(f.World, f.Process, CityCreator(f, notice), new(f.World, f.Process, f.Stacks, f), f.Stacks, random, "accel", era);

    private static void AcceleratedEarlyPackage()
    {
        var f = AccelerationFixture();
        f.AddUnit(1, 1, 0, new(20, 12));
        var random = AccelerationChoices((1, 49, null));
        var operation = Acceleration(f, random);
        while (!operation.Advance()) { }
        random.RequireChoicesConsumed();
        Equal((short)21, f.World.Globals.Turn);
        Equal((short)-4000, f.World.Globals.Year);
        Equal(74, f.World.Civilizations[1].Gold);
        Equal((byte)4, f.World.Civilizations[1].NumAdvances);
        foreach (int a in new[] { 36, 9, 1, 8 })
        {
            Equal(true, f.World.Civilizations[1].Advances.Contains(a));
            Equal((byte)0, f.World.Civilizations[1].AdvanceSource[a]);
            Equal(CivilizationSet.Only(1), f.World.Globals.AdvanceKnownBy[a]);
        }
        Equal(1, f.World.Entities.Cities.End);
        ref var city = ref f.World.Entities.Cities.Storage[0];
        Equal(new MapPoint(20, 12), city.Position);
        Equal((sbyte)3, city.Size);
        Equal("First", city.Name);
        Equal(true, city.Improvements.Contains(BuildingIds.Palace));
        Equal(true, city.Improvements.Contains(BuildingIds.Temple));
        Equal((short)0, f.World.Civilizations[1].TotalCitySize); // Package is not a full turn/census.
        Equal(default(CityWorkSet), city.WorkedTiles);
        Equal((CitySlot?)null, f.Unit(0).Home);
        Equal(new CitySlot(0), f.Unit(1).Home!.Value); // Only the pre-package first stack unit is reassigned.
        Sequence(new byte[] { 0, 0, 4, 3, 15 }, f.World.Entities.Units.Scanned.ToArray().Select(u => u.Type.Value));
        Equal(UnitOrder.Fortified, f.Unit(2).Orders);
        Equal(UnitOrder.Fortified, f.Unit(3).Orders);
        Equal(UnitOrder.None, f.Unit(4).Orders);
        Equal((byte)0, f.Unit(2).MovesUsed);
        Equal(2, f.World.Map.Tiles.ToArray().Count(t => t.Features.HasFlag(MapFeatures.Irrigation)));
        Equal(MapFeatures.Road | MapFeatures.Irrigation, f.World.Map.Remembered(1, new(21, 11)));
        ulong draws = random.Draws;
        Equal(true, operation.Advance()); // Completed polling draws nothing.
        Equal(draws, random.Draws);
    }

    private static void AcceleratedAdvancedPackages()
    {
        foreach (bool fallback in new[] { false, true })
        {
            var f = AccelerationFixture();
            if (fallback)
            {
                foreach (var d in MapNeighborhoods.Directions)
                {
                    var point = new MapPoint(20 + 5 * d.X, 12 + 5 * d.Y);
                    f.World.Map.TileAt(point).Terrain = Terrain.Ocean;
                }
            }
            var random = AccelerationChoices((1, 49, fallback ? null : 1));
            var operation = Acceleration(f, random, 1);
            while (!operation.Advance()) { }
            random.RequireChoicesConsumed();
            Equal((short)41, f.World.Globals.Turn);
            Equal((short)-4000, f.World.Globals.Year);
            Equal((byte)7, f.World.Civilizations[1].NumAdvances); // Three no-choice completions still get the caller increments.
            Equal(fallback ? 173 : 148, f.World.Civilizations[1].Gold);
            Equal(fallback ? 1 : 2, f.World.Entities.Cities.End);
            Equal((sbyte)(fallback ? 7 : 5), f.World.Entities.Cities.Storage[0].Size);
            Equal(true, f.World.Entities.Cities.Storage[0].Improvements.Contains(BuildingIds.Barracks));
            Equal(fallback, f.World.Entities.Cities.Storage[0].Improvements.Contains(BuildingIds.Colosseum));
            if (fallback)
            {
                foreach (int a in new[] { 20, 47, 18, 56, 12, 54 }) Equal(true, f.World.Civilizations[1].Advances.Contains(a));
                Equal(Government.Monarchy, f.World.Civilizations[1].Government);
                Sequence(new byte[] { 0, 4, 3, 15, 16, 4, 5, 16 }, f.World.Entities.Units.Scanned.ToArray().Select(u => u.Type.Value));
            }
            else
            {
                Equal(new MapPoint(25, 7), f.World.Entities.Cities.Storage[1].Position); // First equal score wins, initializer5 admits it.
                Equal((sbyte)3, f.World.Entities.Cities.Storage[1].Size);
                Sequence(new byte[] { 0, 4, 3, 15, 16, 3, 4, 15 }, f.World.Entities.Units.Scanned.ToArray().Select(u => u.Type.Value));
                Equal(UnitOrder.Fortified, f.Unit(5).Orders);
                Equal(UnitOrder.Fortified, f.Unit(6).Orders);
                Equal(false, f.World.Paths.UniformCost);
                Equal(-1, f.World.Paths.Owner);
                Equal(UnitTypeIds.Warriors, f.World.Paths.SelectedUnitType);
                Equal(new MapPoint(20, 12), f.World.Paths.Goal);
            }
        }
    }

    private static void AcceleratedIdentityAndResearch()
    {
        var f = AccelerationFixture();
        foreach (int a in new[] { AdvanceIds.Currency, AdvanceIds.Pottery, AdvanceIds.TheWheel })
        {
            f.Rules.Advances[a] = new() { Enabled = true, AiValue = 10, Prerequisites = new([AdvanceReference.None, AdvanceReference.None]) };
        }
        Equal(6, UnitTypeIds.Pikemen); // F05 fixed identity verified from the original-data mapping.
        f.Rules.Units[6] = f.Rules.Units[3] with { Defense = 8, Identity = new("authored.spears", "Renamed defenders") };
        f.Rules.Units[4] = f.Rules.Units[4] with { Identity = new("authored.archers", "Pikemen") };
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Monarchy).With(AdvanceIds.Polytheism);
        var operation = Acceleration(f, new(17, "research-acceleration"), 1);
        while (!operation.Advance()) { }
        foreach (int a in new[] { AdvanceIds.Currency, AdvanceIds.Pottery, AdvanceIds.TheWheel })
            Equal(true, f.World.Civilizations[1].Advances.Contains(a));
        Equal((byte)7, f.World.Civilizations[1].NumAdvances);
        Equal((byte)6, f.Unit(1).Type.Value);
        Equal((byte)16, f.Unit(3).Type.Value);
        Equal((byte)17, f.Unit(4).Type.Value);
        Equal(Government.Monarchy, f.World.Civilizations[1].Government);
        var buildings = f.World.Entities.Cities.Storage[0].Improvements;
        Equal(true, buildings.Contains(BuildingIds.Granary));
        Equal(true, buildings.Contains(BuildingIds.Marketplace));
        Equal(false, buildings.Contains(BuildingIds.Barracks));
        Equal((CitySlot?)null, f.Unit(0).Home); // A single initial starter is retained without reassignment.
    }

    private static void AcceleratedLastUnitAndRetention()
    {
        foreach (bool nearLast in new[] { false, true })
        {
            var f = AccelerationFixture();
            f.AddUnit(1, 0, 0, nearLast ? new(2, 2) : new(25, 7));
            f.AddUnit(2, 0, 0, nearLast ? new(25, 7) : new(2, 2));
            var operation = Acceleration(f, AccelerationChoices((1, 0, 0)), 1);
            Equal(false, operation.Advance());
            Equal(nearLast ? new MapPoint(30, 12) : new MapPoint(25, 7), f.World.Entities.Cities.Storage[1].Position);
        }
        var retained = AccelerationFixture();
        retained.AddUnit(1, 2, 0, new(20, 2)); // Last measured distance for civ1's D7 is zero.
        var random = AccelerationChoices((1, 0, 0), (2, 0, null));
        var run = Acceleration(retained, random, 1);
        while (!run.Advance()) { }
        random.RequireChoicesConsumed();
        Equal(3, retained.World.Entities.Cities.End);
        Equal(new CivilizationSlot(2), retained.World.Entities.Cities.Storage[2].Owner);
        Equal((sbyte)7, retained.World.Entities.Cities.Storage[2].Size); // No remaining off-city unit: retained zero forces fallback.
        Equal(true, retained.World.Entities.Cities.Storage[2].Improvements.Contains(BuildingIds.Colosseum));
        Equal(false, retained.World.Globals.CivsAlive.Contains(2)); // Captured unit starts do not use alive-set admission.

        var last = AccelerationFixture();
        last.AddUnit(1, 1, 2, new(30, 12)); // Last live unit wins even when not a Settler.
        var early = Acceleration(last, AccelerationChoices((1, 0, null)));
        while (!early.Advance()) { }
        Equal(new MapPoint(30, 12), last.World.Entities.Cities.Storage[0].Position);
    }

    private static void AcceleratedTileDevelopment()
    {
        var f = AccelerationFixture();
        f.AddCity(0, 1, new(20, 12));
        f.World.Entities.Units.SetEnd(0);
        foreach (ref var tile in f.World.Map.Tiles) tile.Features = MapFeatures.Pollution;
        MapPoint first = new(21, 11), second = new(22, 12), hills = new(21, 13);
        f.World.Map.TileAt(first).Features = f.World.Map.TileAt(second).Features = f.World.Map.TileAt(hills).Features = MapFeatures.None;
        f.World.Map.TileAt(hills).Terrain = Terrain.Hills;
        f.World.Map.TileAt(first).Owner = 7; // No ownership gate.
        f.World.Map.TileAt(first).Region = 2; // No region gate on improvements.
        f.World.Map.TileAt(new(25, 11)).Region = 1; // B29, same-region outer land.
        f.World.Map.TileAt(new(20, 14)).Terrain = Terrain.Ocean;
        AcceleratedStarts.DevelopCity(f.World, new(0), 2);
        Equal(MapFeatures.Road | MapFeatures.Irrigation, f.World.Map.TileAt(first).Features);
        Equal(MapFeatures.Road | MapFeatures.Irrigation, f.World.Map.TileAt(second).Features);
        Equal(MapFeatures.None, f.World.Map.TileAt(hills).Features);
        Equal(MapFeatures.Road | MapFeatures.Irrigation, f.World.Map.Remembered(1, first));
        Equal(false, f.World.Map.TileAt(first).RevealedTo.Contains(1)); // Improvement memory did not explore the foreign region.
        Equal(true, f.World.Map.TileAt(new(25, 11)).RevealedTo.Contains(1));
        Equal(MapFeatures.None, f.World.Map.Remembered(1, new(25, 11))); // Exploration did not remember Pollution.
        Equal(false, f.World.Map.TileAt(new(20, 14)).RevealedTo.Contains(1));
        AcceleratedStarts.DevelopCity(f.World, new(0), 2);
        Equal(MapFeatures.Road | MapFeatures.Mine, f.World.Map.TileAt(hills).Features);
        Equal((byte)15, f.World.Map.TileAt(hills).Fertility); // No fertility rebuild.
    }

    private static void AcceleratedRoadsAndPeace()
    {
        var f = AccelerationFixture();
        f.World.Entities.Units.SetEnd(0);
        f.World.Map.TileAt(new(20, 12)).Features = MapFeatures.None;
        f.AddCity(0, 1, new(8, 12));
        f.AddCity(1, 1, new(20, 12));
        f.World.Map.TileAt(new(18, 12)).Features = MapFeatures.Road;
        f.World.Map.TileAt(new(16, 12)).River = true;
        f.World.Map.TileAt(new(14, 12)).Features = MapFeatures.Unit;
        f.World.Map.TileAt(new(14, 12)).Owner = 2;
        f.World.Map.TileAt(new(12, 12)).Claimant = 2;
        f.World.Civilizations[1].Treaties[2].Peace = true;
        Equal((int?)2, TerritoryQueries.PeaceOwner(f.World, new(12, 12), 1));
        Equal((int?)null, TerritoryQueries.PeaceOwner(f.World, new(12, 12), -1));
        Equal((int?)null, TerritoryQueries.PeaceOwner(f.World, new(-1, 12), 1));
        AcceleratedStarts.ConnectRoad(f.World, f.Rules, new(1), new(8, 12), 1);
        Equal(MapFeatures.Road, f.World.Map.TileAt(new(10, 12)).Features); // Blocked edits never stop path progression.
        Equal(MapFeatures.Road, f.World.Map.Remembered(1, new(10, 12)));
        Equal(MapFeatures.None, f.World.Map.Remembered(1, new(18, 12))); // Existing roads are not remembered here.
        foreach (int x in new[] { 8, 12, 14, 16, 20 }) Equal(false, f.World.Map.TileAt(new(x, 12)).Features.HasFlag(MapFeatures.Road));
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.BridgeBuilding);
        f.World.Civilizations[1].Treaties[2].Alliance = true;
        Equal((int?)null, TerritoryQueries.PeaceOwner(f.World, new(12, 12), 1));
        AcceleratedStarts.ConnectRoad(f.World, f.Rules, new(1), new(8, 12), 1);
        Equal(true, f.World.Map.TileAt(new(12, 12)).Features.HasFlag(MapFeatures.Road));
        Equal(true, f.World.Map.TileAt(new(16, 12)).Features.HasFlag(MapFeatures.Road));
        Equal(0, f.World.Paths.ResultCost); // Later local queries reused the flood.
        Equal(false, f.World.Paths.UniformCost);
    }

    private static void AcceleratedFailurePrefixes()
    {
        var f = AccelerationFixture();
        f.World.Entities.Cities.SetEnd(255);
        for (int i = 0; i < 255; ++i) f.World.Entities.Cities.Storage[i].Token = new((uint)i + 1);
        int notices = 0;
        var random = AccelerationChoices();
        var operation = Acceleration(f, random, notice: () => ++notices);
        while (!operation.Advance()) { }
        Equal(1, notices);
        Equal((byte)4, f.World.Civilizations[1].NumAdvances);
        Equal(1, f.World.Entities.Units.End);
        Equal((short)21, f.World.Globals.Turn);
        random.RequireChoicesConsumed(); // Failed first city skips treasury and military package.

        var failed = AccelerationFixture();
        failed.World.Entities.Units.SetEnd(EntityLimits.Units);
        for (int i = 1; i < EntityLimits.Units; ++i)
            failed.World.Entities.Units.Storage[i] = new() { Token = new((uint)i + 1), Position = new(-1, -1), Owner = new(0) };
        // All extra records are off-map barbarians and never participate in the initial owner's stack.
        var run = Acceleration(failed, AccelerationChoices((1, 10, null)));
        Throws<InvalidOperationException>(() => run.Advance());
        Equal(UnitCreationNotice.TooManyUnits, failed.Notices.Single());
        Equal(1, failed.World.Entities.Cities.End);
        Equal((sbyte)3, failed.World.Entities.Cities.Storage[0].Size);
        Equal(35, failed.World.Civilizations[1].Gold);
        Equal((short)0, failed.World.Globals.Turn);
        Throws<InvalidOperationException>(() => run.Advance());
    }

    private static void StartingGrantCityDomains()
    {
        var f = AccelerationFixture();
        f.AddCity(0, 2, new(30, 12));
        ref var civ = ref f.World.Civilizations[1];
        civ.Researching = 5; civ.ResearchProgress = 23; civ.NumAdvances = 17;
        Research.GrantStarting(f.World, 1, 5, -2);
        Equal((short)-1, civ.Researching);
        Equal((short)23, civ.ResearchProgress); // Direct grant does not reset points.
        Equal((byte)254, civ.AdvanceSource[5]);
        Research.GrantStarting(f.World, 1, 5, 0);
        Equal((byte)0, civ.AdvanceSource[5]);
        Equal((byte)17, civ.NumAdvances);
        civ.Researching = 6;
        var random = AccelerationChoices();
        Equal(6, Research.CompleteStarting(f.World, f.Process.Rules, 1, random, "complete")); // Foreign cities are not reset targets.
        Equal((short)0, civ.ResearchProgress);
        f.World.Entities.Cities.Storage[0].Owner = new(1);
        Throws<InvalidOperationException>(() => Research.CompleteStarting(f.World, f.Process.Rules, 1, random, "owned"));
        Research.GrantStarting(f.World, 1, 100);
        Equal((byte)1, civ.FutureTechs); // Direct turn-zero grants permit owned cities.
    }

    private static void InstallAccelerationUnits(RuleTables rules)
    {
        foreach (int type in new[] { 0, 2, 3, 4, 5, 6, 15, 16, 17 })
        {
            rules.Units[type] = new()
            {
                Role = type == 0 ? RuleRole.Settle : type == 3 ? RuleRole.Defend : RuleRole.Attack,
                Prerequisite = AdvanceReference.None,
                CostRows = 2,
                HitPoints = 10,
                Movement = 3,
                Attack = 1,
                Defense = 2
            };
        }
    }

    private static string AccelerationSnapshot(GameState world)
    {
        var units = string.Join("|", world.Entities.Units.Scanned.ToArray().Where(u => u.Token.Occupied)
            .Select(u => $"{u.Owner.Value}:{u.Type.Value}:{u.Position}:{u.Home}:{u.Orders}:{u.Previous}:{u.Next}"));
        var cities = string.Join("|", world.Entities.Cities.Scanned.ToArray().Where(c => c.Token.Occupied)
            .Select(c => $"{c.Owner.Value}:{c.Position}:{c.Size}:{c.Production}:{c.Name}:"
                + string.Join(",", Enumerable.Range(1, 34).Where(c.Improvements.Contains))));
        var tiles = string.Join("|", world.Map.Tiles.ToArray().Select(t => $"{t.Terrain}:{t.Features}:{t.Owner}:{t.Claimant}:"
            + string.Join(",", Enumerable.Range(0, 8).Where(t.RevealedTo.Contains))));
        var civs = string.Join("|", world.Civilizations.Select(c => $"{c.Gold}:{c.NumAdvances}:{c.FutureTechs}:{c.CityCount}:{c.Government}:"
            + string.Join(",", Enumerable.Range(0, 100).Where(c.Advances.Contains))));
        return string.Join(";", units, cities, tiles, civs, string.Join(",", world.Paths.Costs));
    }

    private static void GeneratedAccelerationIntegration()
    {
        foreach (bool flat in new[] { false, true })
            foreach (int era in new[] { 0, 1 })
            {
                using var runtime = new SessionRuntime(new(), new(903));
                string? snapshot = null;
                ulong draws = 0;
                for (int run = 0; run < 2; ++run)
                {
                    runtime.PrepareNewGame();
                    InstallGeneratedStartingRules(runtime);
                    InstallAccelerationUnits(runtime.Process.Rules.Definitions);
                    for (int c = 1; c < 8; ++c) runtime.Process.Rules.Leaders[c].Plural = new("tribes 0");
                    var world = runtime.World;
                    world.WorldSetup.Width = 80; world.WorldSetup.Height = 50;
                    world.Globals.Rules.FlatWorld = flat;
                    world.Globals.Difficulty = Difficulty.King;
                    var starts = new StartingPositions();
                    var random = new GameplayRandom(47, "generated-acceleration");
                    WorldGeneration.Generate(runtime, ref starts, random, "world", new(1234));
                    Equal(true, world.Paths.CachedGoal is not null); // W02's actual retained workspace survives into acceleration.
                    using (var initialization = new GeneratedGameInitialization(runtime, starts, random, "start"))
                    {
                        for (int i = 0; i < 50_000 && !initialization.Completed; ++i) initialization.Poll(run == 0 ? 11 : 251);
                        Equal(true, initialization.Completed);
                    }
                    using var acceleration = new GeneratedGameAcceleration(runtime, random, "accel", era, new(NamingCatalog(["First", "Second"])));
                    for (int i = 0; i < 7 && !acceleration.Completed; ++i) acceleration.Poll();
                    Equal(true, acceleration.Completed);
                    Equal(false, acceleration.Cancelled);
                    Equal(0, acceleration.Notices.Count);
                    Equal((short)(era == 0 ? 21 : 41), world.Globals.Turn);
                    Equal((short)-4000, world.Globals.Year);
                    Equal(true, world.Civilizations[1].CityCount >= 1);
                    for (int c = 1; c < 8; ++c)
                    {
                        if (world.Civilizations[c].CityCount == 0) continue;
                        Equal((byte)(era == 0 ? 4 : 7), world.Civilizations[c].NumAdvances);
                        Equal(true, world.Civilizations[c].CityCount is >= 1 and <= 2);
                    }
                    string result = AccelerationSnapshot(world);
                    if (run == 0) { snapshot = result; draws = random.Draws; }
                    else { Equal(snapshot!, result); Equal(draws, random.Draws); }
                }
            }
    }

    private static void GeneratedAccelerationLifetime()
    {
        using var runtime = new SessionRuntime(new(), new(904));
        void Prepare()
        {
            runtime.PrepareNewGame();
            InstallGeneratedStartingRules(runtime);
            InstallAccelerationUnits(runtime.Process.Rules.Definitions);
            runtime.World.Globals.Year = -4000;
            runtime.World.Map.Allocate(new(40, 24));
            runtime.World.Map.Tiles.Fill(new() { Terrain = Terrain.Plains, Region = 1, ResourceSuppressed = true });
            runtime.World.Entities.Units.SetEnd(1);
            runtime.World.Entities.Units.Storage[0] = new()
            {
                Token = new(1),
                Owner = new(1),
                Type = new(0),
                Position = new(20, 12)
            };
            runtime.World.Map.TileAt(new(20, 12)).Features = MapFeatures.Unit;
            runtime.World.Map.TileAt(new(20, 12)).Owner = 1;
        }
        Prepare();
        var random = AccelerationChoices((1, 0, null));
        using var pending = new GeneratedGameAcceleration(runtime, random, "accel", 0, new(NamingCatalog(["First"])));
        Equal(false, pending.Poll());
        Equal(1, runtime.World.Entities.Cities.End);
        Throws<InvalidOperationException>(() => runtime.PrepareNewGame());
        pending.Dispose();
        Equal(true, pending.Cancelled);
        Equal(false, pending.Poll());
        random.RequireChoicesConsumed();
        var priorPaths = runtime.World.Paths;
        Prepare();
        Equal(false, ReferenceEquals(priorPaths, runtime.World.Paths));
        Equal((MapPoint?)null, runtime.World.Paths.CachedGoal);
        using var failure = new GeneratedGameAcceleration(runtime, AccelerationChoices(), "accel", 0, new(NamingCatalog(["First"])));
        Throws<ChoiceException>(() => failure.Poll());
        Equal(true, failure.Cancelled);
        Equal(1, runtime.World.Entities.Cities.End); // Required treasury choice fails after real city creation.
        Equal((sbyte)1, runtime.World.Entities.Cities.Storage[0].Size);
        Prepare(); // Failure released its world pin.
    }

    private static void GeneratedWorldEntryAcceleration()
    {
        foreach (int choice in new[] { 1, 2 })
        {
            using var f = new EntryFixture();
            int rulesPrompts = 0;
            f.Players.Rules = _ => ++rulesPrompts == 1 ? 1 : 0;
            f.Players.Advanced = defaults => new(0, defaults with { AcceleratedStartup = true });
            f.Players.Acceleration = (_, _) => choice;
            var definitions = new RuleTables();
            InstallAccelerationUnits(definitions);
            var rules = f.Input.Definitions.Units.ToArray();
            foreach (int type in new[] { 0, 2, 3, 4, 5, 6, 15, 16, 17 }) rules[type] = definitions.Units[type];
            f.Input = f.Input with
            {
                Definitions = f.Input.Definitions with
                {
                    Units = new(rules),
                    Advances = new(Enumerable.Range(0, 100).Select(_ => new AdvanceRule
                    {
                        Enabled = true,
                        AiValue = 10,
                        Prerequisites = new([AdvanceReference.None, AdvanceReference.None])
                    }))
                }
            };
            f.Services = f.Services with
            {
                GenerateWorld = null,
                Generation = new(91),
                Initialize = null,
                Accelerate = null,
                Acceleration = new(NamingCatalog(["First", "Second"]))
            };
            Equal(true, f.Run(new GameplayRandom(99, "entry")));
            Equal((short)(choice == 1 ? 21 : 41), f.Runtime.World.Globals.Turn);
            Equal((short)-4000, f.Runtime.World.Globals.Year);
            Equal(true, f.Runtime.World.Civilizations[1].CityCount >= 1);
            Equal(false, f.Trace.Any(x => x.StartsWith("accelerate:", StringComparison.Ordinal) || x.StartsWith("create:", StringComparison.Ordinal)));
            Equal("introduce", f.Trace[^1]);
            f.Runtime.PrepareNewGame();
        }
    }
}
