using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static GameState StartingWorld(int width = 40, int height = 24, bool flat = true)
    {
        var world = GenerationWorld(width, height, flat);
        world.Globals.HumanPlayers = CivilizationSet.Only(1);
        world.Map.Regions[1] = new(240, 64, 0, default);
        world.Map.Tiles.Fill(new() { Terrain = Terrain.Plains, Region = 1, Fertility = 15, Owner = 0 });
        return world;
    }

    private static GameplayRandom SiteChoices(int count, int x = 10, int y = 8, int width = 40, int height = 24)
    {
        var choices = new List<Choice>();
        for (int i = 0; i < count; ++i)
        {
            if (width > 1)
            {
                choices.Add(Choice.Create(new("F05.start-site.x", "site", i),
                    new SamplingLaw.PrimaryModulo(width), ChoiceCode.Primary(x)));
            }
            if (height > 1)
            {
                choices.Add(Choice.Create(new("F05.start-site.y", "site", i),
                    new SamplingLaw.PrimaryModulo(height), ChoiceCode.Primary(y)));
            }
        }
        return new(new ControlledChoices(new(choices)), "starting-sites");
    }

    // U01 nearest-unit / C01 weighted nearest-city: later ties, live holes and declared owner encodings.
    private static void StartingNearestQueries()
    {
        var world = StartingWorld();
        MapPoint origin = new(10, 8);
        Equal((UnitSlot?)null, UnitQueries.Nearest(world, origin).Slot);
        Equal(9999, UnitQueries.Nearest(world, origin).Distance);
        Equal(9999, CityQueries.Nearest(world, origin).Distance);
        var units = world.Entities.Units;
        units.SetEnd(4);
        units.Storage[0] = new() { Token = new(1), Owner = new(1), Position = new(8, 8) };
        units.Storage[1] = new() { Token = new(2), Owner = new(2), Position = new(12, 8) };
        units.Storage[2] = new() { Token = new(3), Owner = new(1), Position = new(10, 10) };
        units.Storage[3] = new() { Position = origin };
        Equal(new UnitSlot(2), UnitQueries.Nearest(world, origin).Slot!.Value);
        Equal(1, UnitQueries.Nearest(world, origin).Distance);
        Equal(new UnitSlot(1), UnitQueries.Nearest(world, origin, excluded: new(2)).Slot!.Value);
        Equal(new UnitSlot(2), UnitQueries.Nearest(world, origin, 257).Slot!.Value);
        Equal(new UnitSlot(1), UnitQueries.Nearest(world, origin, 2).Slot!.Value);
        units.Storage[0].Position = new(-1, -1);
        Equal(new UnitSlot(0), UnitQueries.Nearest(world, new(-1, -1), 1).Slot!.Value);

        var cities = world.Entities.Cities;
        cities.SetEnd(3);
        cities.Storage[0] = new() { Token = new(1), Owner = new(1), Position = new(8, 8) };
        cities.Storage[1] = new() { Token = new(2), Owner = new(2), Position = new(12, 8) };
        cities.Storage[2] = new() { Position = origin };
        Equal(new CitySlot(1), CityQueries.Nearest(world, origin).Slot!.Value);
        Equal(new CitySlot(0), CityQueries.Nearest(world, origin, 257).Slot!.Value);
        Equal(new CitySlot(1), CityQueries.Nearest(world, origin, 1, 2).Slot!.Value);
        Equal(new CitySlot(0), CityQueries.Nearest(world, origin, 1, 258).Slot!.Value);
    }

    // F05 §7: thresholds use the incremented attempt, with separate claimant/fertility and signed regions.
    private static void StartingSearchRelaxation()
    {
        var world = StartingWorld();
        world.Map.TileAt(new(10, 8)).Fertility = 11;
        var random = SiteChoices(32);
        var search = new StartingSiteSearch(world, 1, new int[64], random, "site");
        Equal((StartingSite?)null, search.Advance(31));
        Equal(new StartingSite(new(10, 8), 32), search.Advance(1)!.Value);
        random.RequireChoicesConsumed();
        var state = random.Capture();
        Equal(search.Result, search.Advance(20));
        Equal(state.Position, random.Capture().Position);
        Equal(64, random.Capture().Trace!.Events.Count);

        world.Map.TileAt(new(10, 8)).Fertility = 15;
        world.Map.Regions[1] = new(240, 31, 0, default);
        random = SiteChoices(64);
        search = new(world, 1, new int[64], random, "site");
        Equal((StartingSite?)null, search.Advance(63));
        Equal(64, search.Advance(1)!.Value.Attempts);
        random.RequireChoicesConsumed();

        world.Map.Regions[1] = new(240, 64, 0, default);
        world.Entities.Units.SetEnd(1);
        world.Entities.Units.Storage[0] = new() { Token = new(1), Position = new(30, 8) };
        random = SiteChoices(128);
        search = new(world, 1, new int[64], random, "site");
        Equal((StartingSite?)null, search.Advance(127));
        Equal(128, search.Advance(1)!.Value.Attempts);
        random.RequireChoicesConsumed();

        world.Entities.Units.SetEnd(0);
        world.Map.TileAt(new(10, 8)).Claimant = 2;
        random = SiteChoices(320);
        search = new(world, 1, new int[64], random, "site");
        Equal((StartingSite?)null, search.Advance(319));
        Equal(320, search.Advance(1)!.Value.Attempts);
        random.RequireChoicesConsumed();

        world.Map.TileAt(new(10, 8)).Claimant = 0;
        world.Map.TileAt(new(10, 8)).Fertility = 1;
        random = SiteChoices(128);
        search = new(world, 1, new int[64], random, "site");
        Equal(128, search.Advance(128)!.Value.Attempts);
        random.RequireChoicesConsumed();
    }

    // F05 §7: the first 1500 flat candidates are inset; cylindrical and small axes are independent.
    private static void StartingSearchCoordinates()
    {
        var world = StartingWorld(82, 40);
        world.Map.Tiles.Fill(new() { Terrain = Terrain.Ocean, Region = 1, Owner = 0 });
        world.Map.TileAt(new(1, 1)).Terrain = Terrain.Plains;
        var choices = new List<Choice>();
        for (int i = 0; i < 1500; ++i)
        {
            choices.Add(Choice.Create(new("F05.start-site.x", "site", i),
                new SamplingLaw.PrimaryModulo(66), ChoiceCode.Primary(0)));
            choices.Add(Choice.Create(new("F05.start-site.y", "site", i),
                new SamplingLaw.PrimaryModulo(24), ChoiceCode.Primary(0)));
        }
        choices.Add(Choice.Create(new("F05.start-site.x", "site", 1500),
            new SamplingLaw.PrimaryModulo(82), ChoiceCode.Primary(0)));
        choices.Add(Choice.Create(new("F05.start-site.y", "site", 1500),
            new SamplingLaw.PrimaryModulo(40), ChoiceCode.Primary(1)));
        var random = new GameplayRandom(new ControlledChoices(new(choices)), "starting-sites");
        var search = new StartingSiteSearch(world, 1, new int[64], random, "site");
        Equal((StartingSite?)null, search.Advance(1500));
        Equal(new StartingSite(new(1, 1), 1501), search.Advance(1)!.Value);
        random.RequireChoicesConsumed();
        var events = random.Capture().Trace!.Events;
        Equal("F05.start-site.x", events[0].Key.Site);
        Equal("F05.start-site.y", events[1].Key.Site);

        world = StartingWorld(82, 41, false);
        random = SiteChoices(1, 0, 9, 82, 41);
        search = new(world, 1, new int[64], random, "site");
        Equal(new StartingSite(new(1, 9), 1), search.Advance(1)!.Value);
        random.RequireChoicesConsumed();

        world = StartingWorld(2, 1);
        random = SiteChoices(1, 1, 0, 2, 1);
        search = new(world, 1, new int[64], random, "site");
        Equal(new StartingSite(new(0, 0), 1), search.Advance(1)!.Value);
        random.RequireChoicesConsumed();
    }

    // F05 §7 / V01 §2: AI returns unchecked attempt 2001; human turn zero has no cap, and hints are later.
    private static void StartingSearchLimitsAndHints()
    {
        var world = StartingWorld();
        ref var tile = ref world.Map.TileAt(new(10, 8));
        tile.Terrain = Terrain.Ocean;
        tile.Features = MapFeatures.Unit;
        var random = SiteChoices(2001);
        var search = new StartingSiteSearch(world, 2, new int[64], random, "site");
        Equal((StartingSite?)null, search.Advance(2000));
        Equal(new StartingSite(new(10, 8), 2001), search.Advance(1)!.Value);
        random.RequireChoicesConsumed();
        Equal(new StartingSite(new(0, 1), 0), StartingSites.ApplyHint(world, search.Result!.Value, new(0, 1)));
        Equal(search.Result.Value, StartingSites.ApplyHint(world, search.Result.Value, new(-1, 0)));
        world.Globals.Turn = 1;
        Equal(search.Result.Value, StartingSites.ApplyHint(world, search.Result.Value, new(0, 1)));

        world.Globals.Turn = 0;
        random = SiteChoices(2002);
        search = new(world, 1, new int[64], random, "site");
        Equal((StartingSite?)null, search.Advance(2001));
        tile.Terrain = Terrain.Plains;
        tile.Features = default;
        Equal(2002, search.Advance(1)!.Value.Attempts);
        random.RequireChoicesConsumed();

        world.Entities.Units.SetEnd(1);
        world.Entities.Units.Storage[0] = new() { Token = new(1), Position = new(12, 8) };
        random = SiteChoices(2001);
        search = new(world, 1, new int[64], random, "site");
        Equal((StartingSite?)null, search.Advance(2000));
        Equal(2001, search.Advance(1)!.Value.Attempts);
        random.RequireChoicesConsumed();

        world.Globals.Turn = 201;
        var captured = new int[64];
        captured[1] = 1;
        random = SiteChoices(2001);
        search = new(world, 1, captured, random, "site");
        captured[1] = 0;
        Equal((StartingSite?)null, search.Advance(2000));
        Equal(2001, search.Advance(1)!.Value.Attempts);
        random.RequireChoicesConsumed();
    }

    // F05 §8: score all captured selections before grants, use rivers (not specials), and include dead seats.
    private static void StartingScoreContract()
    {
        var world = StartingWorld(100, 60);
        world.Globals.CivsAlive = CivilizationSet.Only(1);
        world.Map.Tiles.Fill(new() { Terrain = Terrain.Grassland, Region = 1 });
        world.Map.Regions[1] = new(250, 0, 0, default);
        world.Map.Regions[2] = new(100, 0, 0, default);
        var units = world.Entities.Units;
        units.SetEnd(4);
        units.Storage[0] = new() { Token = new(1), Owner = new(1), Position = new(20, 20) };
        units.Storage[1] = new() { Token = new(2), Owner = new(2), Position = new(40, 20) };
        units.Storage[2] = new() { Token = new(3), Owner = new(3), Position = new(70, 40) };
        units.Storage[3] = new() { Token = new(4), Owner = new(1), Position = new(0, 0) };
        world.Map.TileAt(new(70, 40)).Region = 2;
        var scores = StartingSites.Assess(world);
        Equal(new UnitSlot(0), scores.Units[1]!.Value);
        Equal(6, scores.Scores[1]);
        Equal(6, scores.Scores[2]);
        Equal(7, scores.Scores[3]);
        Equal(7, scores.Maximum);
        Equal((UnitSlot?)null, scores.Units[4]);
        Equal(0, scores.Scores[4]);

        // The center is the fourth river tile; four resources elsewhere do not replace this count.
        foreach (int i in new[] { 0, 1, 2, 20 })
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, new(20, 20), MapNeighborhoods.Radius[i], true);
            world.Map.TileAt(point).River = true;
        }
        var rivers = StartingSites.Assess(world);
        Equal(8, rivers.Scores[1]);
        Equal(8, rivers.Maximum);
        Equal(6, scores.Scores[1]);
        units.Storage[0].Token = default;
        Equal(new UnitSlot(0), scores.Units[1]!.Value);

        world.Map.Regions[2] = new(-100, 0, 0, default);
        Equal(8, StartingSites.Assess(world).Scores[3]);
    }

    // V01 §2 and Session ownership: usable hints never bypass a pending search; completion/cancel unpin.
    private static void StartingSearchSessionLifetime()
    {
        using var runtime = new SessionRuntime(new(), new(700));
        var world = runtime.World;
        world.Globals.HumanPlayers = CivilizationSet.Only(1);
        world.Map.Allocate(new(40, 24));
        world.Map.Regions[1] = new(240, 64, 0, default);
        world.Map.Tiles.Fill(new() { Terrain = Terrain.Ocean, Region = 1, Fertility = 15, Owner = 0 });
        var hints = new StartingPositions();
        hints.Positions[0] = new(0, 1);
        var random = SiteChoices(2);
        using var operation = new StartingPositionOperation(runtime, 1, hints, new int[64], random, "site");
        hints.Positions[0] = new(2, 2);
        Equal((StartingSite?)null, operation.Poll(1));
        Throws<InvalidOperationException>(runtime.PrepareNewGame);
        Equal(2, random.Capture().Trace!.Events.Count);
        world.Map.TileAt(new(10, 8)).Terrain = Terrain.Plains;
        Equal(new StartingSite(new(0, 1), 0), operation.Poll(1)!.Value);
        random.RequireChoicesConsumed();
        Equal(operation.Result, operation.Poll());
        Equal(false, operation.Cancelled);
        runtime.PrepareNewGame();

        runtime.World.Map.Allocate(new(40, 24));
        random = SiteChoices(1);
        using var cancelled = new StartingPositionOperation(runtime, 1, new(), new int[64], random, "site");
        cancelled.Poll(1);
        cancelled.Dispose();
        Equal(true, cancelled.Cancelled);
        Equal((StartingSite?)null, cancelled.Poll());
        random.RequireChoicesConsumed();
        runtime.PrepareNewGame();

        runtime.World.Map.Allocate(new(40, 24));
        using var failed = new StartingPositionOperation(runtime, 1, new(), new int[64], SiteChoices(0), "site");
        Throws<ChoiceException>(() => failed.Poll());
        Equal(true, failed.Cancelled);
        runtime.PrepareNewGame();
    }

    // F05/W02 real producer join: preset/custom generated maps, both topologies and a second setup in one runtime.
    // Repeated results are same-policy regression evidence, not independently observed original starts.
    private static void StartingGeneratedWorldIntegration()
    {
        foreach (bool flat in new[] { false, true })
        {
            foreach (var (width, height) in new[] { (80, 50), (100, 80), (150, 120), (40, 51), (500, 40) })
            {
                using var runtime = new SessionRuntime(new(), new(701));
                StartingSite? first = null;
                ulong draws = 0;
                for (int game = 0; game < 2; ++game)
                {
                    runtime.PrepareNewGame();
                    var world = runtime.World;
                    world.WorldSetup.Width = (short)width;
                    world.WorldSetup.Height = (short)height;
                    world.Globals.Rules.FlatWorld = flat;
                    world.Globals.HumanPlayers = CivilizationSet.Only(1);
                    var hints = new StartingPositions();
                    var random = new GameplayRandom(17, "generated-start");
                    MapGenerator.Generate(world, ref hints, GenerationRules(), random, "world", 1234);
                    using var search = new StartingPositionOperation(runtime, 1, hints, new int[64], random, "start");
                    for (int batch = 0; batch < 1000 && search.Result is null; ++batch)
                    {
                        search.Poll(64);
                    }
                    Equal(true, search.Result.HasValue);
                    var result = search.Result!.Value;
                    Equal(true, world.Map.Geometry.Canonical(result.Position));
                    Equal(false, world.Map.TileAt(result.Position).Ocean);
                    Equal((byte?)null, world.Map.CityOrUnitOwner(result.Position));
                    Equal(false, result.Attempts <= 2000 && world.Map.Hut(result.Position));
                    if (game == 0)
                    {
                        first = result;
                        draws = random.Draws;
                    }
                    else
                    {
                        Equal(first!.Value, result);
                        Equal(draws, random.Draws);
                    }
                    world.Civilizations[1].Advances = default(AdvanceSet).With(5);
                    world.Map.TileAt(result.Position).Claimant = 7;
                }
            }
        }
    }
}
