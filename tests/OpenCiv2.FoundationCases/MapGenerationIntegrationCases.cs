using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // W02 Land creation: wrong-parity starts alias only at lookup; repeated pass coverage wraps at 256.
    private static void GenerationLandWalks()
    {
        var world = GenerationWorld(40, 30);
        var input = new List<(string, int, int)>();
        for (int pass = 0; pass < 2; ++pass)
            input.AddRange([("land.start-x", 24, 0), ("land.start-y", 22, 1),
                ("land.overlap", 2, 1), ("land.length", 64, 0), ("land.direction", 4, 0)]);
        var random = GenerationChoices(input.ToArray());
        var phase = GenerationPhase(world, random);
        phase.Landmass(false);
        int aliased = world.Map.Geometry.TileIndex(new(9, 5))!.Value;
        Equal(3, phase.Placed);
        Equal((byte)1, phase.Coverage[aliased]);
        Equal((byte)1, phase.Coverage[world.Map.Geometry.TileIndex(new(8, 4))!.Value]);
        phase.Coverage[aliased] = 255;
        phase.Landmass(false);
        Equal(6, phase.Placed);
        Equal((byte)0, phase.Coverage[aliased]);
        Equal((byte)2, phase.Coverage[world.Map.Geometry.TileIndex(new(8, 6))!.Value]);
        Equal(true, world.Map.Tiles.ToArray().All(t => t.Ocean));
        random.RequireChoicesConsumed();
    }

    // W02 Common finalization: retained visibility/radius/memory/seed/starts, cleared features/owners,
    // marker-aware suppression, and final polar edits deliberately leave earlier region counts intact.
    private static void GenerationRetainedFinalization()
    {
        foreach (bool flat in new[] { false, true })
            foreach (bool wide in new[] { false, true })
            {
                var world = GenerationWorld(40, 51, flat);
                var map = world.Map;
                map.PatternSeed = 24;
                map.Tiles.Fill(new Tile
                {
                    Features = MapFeatures.Road | MapFeatures.Unit,
                    Claimant = 3,
                    RevealedTo = new CivilizationSet().With(2),
                    Owner = 4
                });
                map.RememberedLayer(2).Fill(MapFeatures.Mine);
                var imported = map.Tiles.ToArray().Select(StateConversion.ToStored).ToArray();
                int marked = Enumerable.Range(0, map.Geometry.Area).First(i =>
                    map.Geometry.Coordinates(i).Y > 5 && map.SpecialResource(map.Geometry.Coordinates(i)) != 0);
                imported[marked] = imported[marked] with { TerrainFlags = 0x30, RadiusOther = 7 };
                imported[0] = imported[0] with { TerrainFlags = 0x10, RadiusOther = 9 };
                var compatibility = new MapCompatibility(map, imported);
                var starts = new StartingPositions();
                starts.Positions[0] = new(3, 5);
                var random = GenerationChoices();
                MapGenerator.Generate(world, ref starts, GenerationRules(), random, "phase", 999, true, wide,
                    markers: compatibility.GenerationMarkers(map));
                random.RequireChoicesConsumed();
                Equal((ushort)24, map.PatternSeed);
                Equal(new MapPoint(3, 5), starts.Positions[0]);
                Equal(true, map.Tiles.ToArray().All(t => t.Features == MapFeatures.None && t.Owner is null
                    && t.Claimant == 3 && t.RevealedTo.Contains(2)));
                Equal(true, map.RememberedLayer(2).ToArray().All(t => t == MapFeatures.Mine));
                Equal((byte)0x30, compatibility.Export(map, marked).TerrainFlags);
                Equal((byte)7, compatibility.Export(map, marked).RadiusOther);
                Equal((byte)(flat ? 0x10 : 0), compatibility.Export(map, 0).TerrainFlags);
                Equal((byte)9, compatibility.Export(map, 0).RadiusOther);
                Equal(!wide, map.Tiles.ToArray().Any(t => t.ResourceSuppressed));
                Equal((short)map.Geometry.Area, map.Regions[1].Water); // all ocean at labeling
                Equal(0, map.Regions.ToArray().Sum(r => r.Land));
                Equal(flat ? Terrain.Ocean : Terrain.Glacier, map.Tiles[0].Terrain);
                Equal(map.Tiles[0], map.TileAt(new(-1, -1)));
                Equal(world.WorldSetup.Parameters[2], 0); // retain does not double parameters
            }
    }

    private static void CompareGeneratedMaps(MapStorage first, MapStorage second)
    {
        Equal(first.PatternSeed, second.PatternSeed);
        Equal(first.StoredTopology, second.StoredTopology);
        Equal(new OwnedList<Tile>(first.Tiles.ToArray()), new(second.Tiles.ToArray()));
        Equal(new OwnedList<RegionSummary>(first.Regions.ToArray()), new(second.Regions.ToArray()));
        for (int graph = 0; graph < 2; ++graph)
            Equal(new OwnedList<byte?>(first.CoarseGrid(graph).ToArray()), new(second.CoarseGrid(graph).ToArray()));
    }

    // W02 complete pipeline, F01 same-policy replay. This is regression evidence, not original-game goldens.
    private static void GenerationControlledReplay()
    {
        var world = GenerationWorld();
        world.Map.Tiles.Fill(new Tile { Terrain = Terrain.Glacier, Claimant = 6, Features = MapFeatures.CitySite });
        world.Map.RememberedLayer(1).Fill(MapFeatures.Road);
        var starts = new StartingPositions();
        starts.Positions[20] = new(19, 19);
        var random = new GameplayRandom(314159, "generation");
        random.RecordChoices(500000);
        var rules = GenerationRules();
        var paths = new GenerationPathWorkspace();
        MapGenerator.Generate(world, ref starts, rules, random, "complete", 32768, paths: paths);
        Equal(new StartingPositions(), starts);
        Equal((ushort)1, world.Map.PatternSeed);
        Equal(true, world.Map.Tiles.ToArray().All(t => t.Claimant == 0 && t.RevealedTo == default));
        Equal(true, world.Map.RememberedLayer(1).ToArray().All(t => t == MapFeatures.None));
        Equal(true, paths.CachedGoal.HasValue);
        var choices = random.Capture().Trace!.Events;
        Equal(63, choices.Count(c => c.Key.Site == "W02.islet.walks"));
        var controlled = new GameplayRandom(new ControlledChoices(choices), "generation");
        var replay = GenerationWorld();
        MapGenerator.Generate(replay, ref starts, rules, controlled, "complete", 32768);
        controlled.RequireChoicesConsumed();
        CompareGeneratedMaps(world.Map, replay.Map);
    }

    // W02 specified generation matrix: all sizes, topologies, land forms/ages and dry/cold/wet/warm extremes.
    private static void GenerationSizeAndParameterMatrix()
    {
        var scenarios = new List<(int W, int H, int Form, int Age, int Mass, int Climate, int Temperature)>();
        foreach (var (w, h) in new[] { (80, 50), (100, 80), (150, 120), (80, 51), (40, 50),
            (100, 20), (500, 40), (80, 250) }) scenarios.Add((w, h, 0, 0, 0, 0, 0));
        for (int form = -1; form <= 1; ++form)
            for (int age = -1; age <= 1; ++age)
                foreach (int extreme in new[] { -1, 1 }) scenarios.Add((80, 50, form, age, extreme, extreme, extreme));
        ulong seed = 1;
        foreach (bool flat in new[] { false, true })
            foreach (var s in scenarios)
            {
                var world = GenerationWorld(s.W, s.H, flat);
                world.WorldSetup.Parameters[0] = s.Mass;
                world.WorldSetup.Parameters[1] = s.Form;
                world.WorldSetup.Parameters[2] = s.Temperature;
                world.WorldSetup.Parameters[3] = s.Climate;
                world.WorldSetup.Parameters[4] = s.Age;
                var starts = new StartingPositions();
                var random = new GameplayRandom(seed++, "matrix");
                MapGenerator.Generate(world, ref starts, GenerationRules(), random, "generate", 1234);
                Equal(s.Temperature * (flat ? 2 : 1), world.WorldSetup.Parameters[2]);
                Equal(s.Climate * (flat ? 2 : 1), world.WorldSetup.Parameters[3]);
                var tiles = world.Map.Tiles.ToArray();
                Equal(true, tiles.Any(t => t.Ocean));
                Equal(true, tiles.Any(t => !t.Ocean));
                Equal(true, tiles.All(t => t.Terrain <= Terrain.Ocean && t.Region < 64 && t.Fertility <= 15
                    && t.Owner is null && t.Features == MapFeatures.None));
                Equal(true, world.Map.CoarseGrid(0).ToArray().All(v => v.HasValue));
                Equal(true, world.Map.CoarseGrid(1).ToArray().All(v => v.HasValue));
                if (!flat)
                    Equal(true, tiles.Take(s.W / 2).Concat(tiles.TakeLast(s.W / 2))
                        .All(t => t.Terrain is Terrain.Glacier or Terrain.Tundra));
            }
    }

    // F05 world-entry integration with production W02; a previously used runtime equals a cold start.
    private static void GenerationWorldEntryIntegration()
    {
        using var fresh = new EntryFixture();
        using var reused = new EntryFixture();
        fresh.Services = fresh.Services with { GenerateWorld = null, Generation = new(91) };
        reused.Services = reused.Services with { GenerateWorld = null, Generation = new(91) };
        Equal(true, reused.Run(new GameplayRandom(12, "entry")));
        reused.Runtime.World.Map.Tiles[0].Features = MapFeatures.Unit;
        reused.Runtime.Process.Fallback.Value.Owner = 7;
        Equal(true, fresh.Run(new GameplayRandom(99, "entry")));
        Equal(true, reused.Run(new GameplayRandom(99, "entry")));
        CompareGeneratedMaps(fresh.Runtime.World.Map, reused.Runtime.World.Map);
        Equal(fresh.Runtime.Process.Fallback.Value, reused.Runtime.Process.Fallback.Value);
        Equal(fresh.Runtime.World.WorldSetup, reused.Runtime.World.WorldSetup);
        Equal("introduce", reused.Trace[^1]);
    }

    private static void GenerationAdmission()
    {
        var world = GenerationWorld();
        var identity = world.Map.StorageIdentity;
        var random = new GameplayRandom(1, "admission");
        var before = random.Capture();
        var starts = new StartingPositions();
        world.WorldSetup.Parameters[1] = 2;
        Throws<ArgumentOutOfRangeException>(() => MapGenerator.Generate(world, ref starts, GenerationRules(), random, "bad", 1));
        Equal(identity, world.Map.StorageIdentity);
        Equal(before, random.Capture());
        world.WorldSetup.Parameters[1] = 0;
        world.WorldSetup.Width = 20;
        Throws<ArgumentOutOfRangeException>(() => MapGenerator.Generate(world, ref starts, GenerationRules(), random, "bad", 1));
        Equal(identity, world.Map.StorageIdentity);
        Equal(before, random.Capture());
    }
}
