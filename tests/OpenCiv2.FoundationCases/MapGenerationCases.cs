using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static GameState GenerationWorld(int width = 80, int height = 50, bool flat = false)
    {
        var world = new GameState(new ProcessContext());
        world.WorldSetup.Width = (short)width;
        world.WorldSetup.Height = (short)height;
        world.Globals.Rules.FlatWorld = flat;
        world.Map.Allocate(new(width, height));
        return world;
    }

    // Authored rules, not stock data or a source-derived harness.
    private static RuleTables GenerationRules()
    {
        var rules = new RuleTables { Cosmic = new() { RoadMovementMultiplier = 3 } };
        rules.Units[2] = new() { Domain = RuleDomain.Land };
        rules.Units[33] = new() { Domain = RuleDomain.Sea };
        for (int i = 0; i < rules.Terrain.Length; ++i)
            rules.Terrain[i] = new() { Food = 1, Shields = 1, Trade = 1, Work = i < 11 ? new() : null };
        return rules;
    }

    private static GameplayRandom GenerationChoices(params (string Site, int N, int Value)[] input)
    {
        var ordinals = new Dictionary<string, int>();
        var choices = new List<Choice>();
        foreach (var (site, n, value) in input)
        {
            int ordinal = ordinals.GetValueOrDefault(site);
            ordinals[site] = ordinal + 1;
            choices.Add(Choice.Create(new("W02." + site, "phase", ordinal),
                new SamplingLaw.PrimaryModulo(n), ChoiceCode.Primary(value)));
        }
        return new(new ControlledChoices(new(choices)), "generation-tests");
    }

    private static MapGeneration GenerationPhase(GameState world, GameplayRandom? random = null) =>
        new(world.Map, world.Globals.Rules.FlatWorld, world.WorldSetup, random ?? GenerationChoices(), "phase");

    // W02 Initial terrain: independent sign/value choices; repeated coverage consumes no choices.
    private static void GenerationLatitudeAndCoverage()
    {
        var world = GenerationWorld(20, 24);
        var random = GenerationChoices(("terrain.sign", 16, 15), ("terrain.latitude", 16, 0));
        var generation = GenerationPhase(world, random);
        int index = world.Map.Geometry.TileIndex(new(0, 12))!.Value;
        generation.Coverage[index] = 1;
        generation.Coverage[index + 1] = 2;
        generation.Coverage[index + 2] = 255;
        generation.InitialTerrain();
        Equal(Terrain.Desert, world.Map.Tiles[index].Terrain);
        Equal(Terrain.Hills, world.Map.Tiles[index + 1].Terrain);
        Equal(Terrain.Mountains, world.Map.Tiles[index + 2].Terrain);
        Equal(Terrain.Ocean, world.Map.Tiles[0].Terrain);
        random.RequireChoicesConsumed();
    }

    // W02 Contact repair: both opposite-pair cases, overwrite coverage, leave west intact.
    private static void GenerationContactRepair()
    {
        foreach (bool northSouth in new[] { false, true })
        {
            var world = GenerationWorld(10, 6);
            var phase = GenerationPhase(world);
            int Index(int x, int y) => world.Map.Geometry.TileIndex(new(x, y))!.Value;
            phase.Coverage[Index(northSouth ? 2 : 1, northSouth ? 0 : 1)] = 7;
            phase.Coverage[Index(northSouth ? 2 : 3, northSouth ? 2 : 1)] = 3;
            phase.RepairContacts();
            Equal((byte)(northSouth ? 0 : 7), phase.Coverage[Index(1, 1)]);
            Equal((byte)1, phase.Coverage[Index(2, 0)]);
            Equal((byte)1, phase.Coverage[Index(2, 2)]);
            Equal((byte)1, phase.Coverage[Index(3, 1)]);
            Equal(0, phase.Placed);
        }
    }

    // W02 Rainfall: odd middle row, reverse sees forward edits, row 2 sees earlier reverse edits.
    // Effective wet-flat climate skips reverse-loss choices; subtraction may cross zero before a transition.
    private static void GenerationAlternatingRainfall()
    {
        var world = GenerationWorld(6, 3, true);
        world.WorldSetup.Parameters[3] = 2;
        foreach (int i in new[] { 1, 3, 5, 7 }) world.Map.Tiles[i].Terrain = Terrain.Plains;
        var random = GenerationChoices(("rain.forward-start", 14, 0), ("rain.forward-start", 13, 0),
            ("rain.forward-start", 14, 1), ("rain.forward-loss", 3, 2),
            ("rain.forward-loss", 3, 0), ("rain.forward-loss", 3, 0));
        GenerationPhase(world, random).Rainfall();
        Equal(new OwnedList<Terrain>([Terrain.Ocean, Terrain.Forest, Terrain.Ocean, Terrain.Grassland,
            Terrain.Ocean, Terrain.Grassland, Terrain.Ocean, Terrain.Grassland, Terrain.Ocean]),
            new(world.Map.Tiles.ToArray().Select(t => t.Terrain)));
        random.RequireChoicesConsumed();
    }

    // W02 Weathering/voting: cardinal lake gate, running negative high-terrain bias and ordered ties.
    private static void GenerationWeatherAndVotes()
    {
        var world = GenerationWorld(20, 20, true);
        var point = new MapPoint(10, 10);
        var phase = GenerationPhase(world);
        world.Map.TileAt(point).Terrain = Terrain.Mountains;
        foreach (int d in new[] { 1, 3, 5, 7 })
            world.Map.TileAt(MapNeighborhoods.Offset(world.Map.Geometry, point, MapNeighborhoods.Directions[d], true)).Terrain = Terrain.Plains;
        phase.Weather(point);
        Equal(Terrain.Ocean, world.Map.TileAt(point).Terrain); // diagonal ocean does not prevent the lake
        world.Map.TileAt(point).Terrain = Terrain.Mountains;
        world.Map.TileAt(new(12, 10)).Terrain = Terrain.Ocean;
        phase.Weather(point);
        Equal(Terrain.Mountains, world.Map.TileAt(point).Terrain);

        world.Map.Tiles.Fill(new Tile());
        world.Map.TileAt(point).Terrain = Terrain.Forest;
        world.WorldSetup.Parameters[3] = -1;
        world.Map.TileAt(new(11, 9)).Terrain = Terrain.Swamp;
        Terrain? last = null;
        GenerationPhase(world).Vote(point, ref last);
        Equal(Terrain.Swamp, last!.Value); // -1 bias is nonzero, so the high-terrain bonus applies
        world.Map.Tiles.Fill(new Tile());
        world.WorldSetup.Parameters[3] = 0;
        world.Map.TileAt(point).Terrain = Terrain.Plains;
        world.Map.TileAt(new(11, 9)).Terrain = Terrain.Forest;
        world.Map.TileAt(new(11, 11)).Terrain = Terrain.Hills;
        last = null;
        GenerationPhase(world).Vote(point, ref last);
        Equal(Terrain.Forest, last!.Value);
        world.Map.TileAt(new(11, 9)).Terrain = Terrain.Desert;
        GenerationPhase(world).Vote(point, ref last);
        Equal(Terrain.Forest, world.Map.TileAt(point).Terrain); // paired last-kind skip
        world.Map.Tiles.Fill(new Tile());
        world.Map.TileAt(point).Terrain = Terrain.Plains;
        last = null;
        GenerationPhase(world).Vote(point, ref last);
        Equal<Terrain?>(null, last);
        Equal(Terrain.Plains, world.Map.TileAt(point).Terrain);

        world.Map.Tiles.Fill(new Tile());
        world.WorldSetup.Parameters[4] = -1;
        world.Map.TileAt(point).Terrain = Terrain.Desert;
        world.Map.TileAt(new(12, 10)).Terrain = Terrain.Plains;
        var paired = new List<(string, int, int)>();
        for (int pair = 0; pair < 400; ++pair)
        {
            paired.Add(("weather.y", 20, pair == 0 ? 10 : 0));
            paired.Add(("weather.x", 10, pair == 0 ? 5 : 0));
            paired.Add(("weather.neighbor", 8, pair == 0 ? 1 : 6));
        }
        var pairedRandom = GenerationChoices(paired.ToArray());
        GenerationPhase(world, pairedRandom).Weathering();
        pairedRandom.RequireChoicesConsumed();
        Equal(Terrain.Plains, world.Map.TileAt(point).Terrain);
        Equal(Terrain.Hills, world.Map.TileAt(new(12, 10)).Terrain);
    }

    // W02 Rivers: boundary-exit acceptance, attempt-800 threshold, complete rollback and backup crossings.
    private static void GenerationRiverRollbackAndBoundary()
    {
        foreach (int attempt in new[] { 799, 800 })
        {
            var world = GenerationWorld(20, 10, true);
            world.Map.Tiles.Fill(new Tile { Terrain = Terrain.Grassland });
            if (attempt == 800)
            {
                world.Map.TileAt(new(3, 3)).Terrain = Terrain.Mountains;
                world.Map.TileAt(new(3, 3)).River = true; // marked mountains are admitted river starts
            }
            var input = new List<(string, int, int)> { ("river.start-y", 10, 3), ("river.start-x", 10, 1), ("river.direction", 4, 3) };
            for (int i = 1; i <= 4; ++i) input.Add(("river.turn", 2, i % 2));
            var random = GenerationChoices(input.ToArray());
            var result = GenerationPhase(world, random).RiverAttempt(attempt);
            Equal((attempt == 800, 4), result);
            Equal(attempt == 800 ? 4 : 0, world.Map.Tiles.ToArray().Count(t => t.River));
            if (attempt == 800) Equal(Terrain.Mountains, world.Map.TileAt(new(3, 3)).Terrain);
            random.RequireChoicesConsumed();
        }
        var loop = GenerationWorld(20, 12, true);
        loop.Map.Tiles.Fill(new Tile { Terrain = Terrain.Grassland });
        loop.Map.TileAt(new(2, 2)).Terrain = Terrain.Ocean;
        var choices = new List<(string, int, int)> { ("river.start-y", 12, 4), ("river.start-x", 10, 2), ("river.direction", 4, 0) };
        for (int i = 1; i <= 10; ++i) choices.Add(("river.turn", 2, i == 9 ? 0 : 1));
        var loopRandom = GenerationChoices(choices.ToArray());
        Equal((true, 10), GenerationPhase(loop, loopRandom).RiverAttempt(1));
        Equal(9, loop.Map.Tiles.ToArray().Count(t => t.River));
        loopRandom.RequireChoicesConsumed();
    }

    // W01 §7: water size cutoff, cylindrical row carry, numeric IDs and nonaggregated overflow.
    private static void GenerationRegionLabels()
    {
        foreach (int height in new[] { 8, 9 })
        {
            var map = GenerationWorld(2, height, true).Map;
            map.Regions[1] = new(0, 17, 0, default);
            Equal((false, false), MapRegions.Rebuild(map, true));
            Equal((byte)(height == 8 ? 63 : 1), map.Tiles[0].Region);
            Equal((short)height, map.Regions[height == 8 ? 63 : 1].Water);
            Equal((short)17, map.Regions[1].Fertile);
        }
        foreach (bool flat in new[] { true, false })
        {
            var map = GenerationWorld(6, 4, flat).Map;
            map.TileAt(new(4, 0)).Terrain = map.TileAt(new(1, 1)).Terrain = Terrain.Grassland;
            MapRegions.Rebuild(map, flat);
            Equal(!flat, map.TileAt(new(4, 0)).Region == map.TileAt(new(1, 1)).Region);
        }
        var many = GenerationWorld(100, 80, true).Map;
        for (int i = 0; i < 65; ++i) many.TileAt(new(2 + 4 * (i % 20), 2 + 4 * (i / 20))).Terrain = Terrain.Plains;
        Equal((true, false), MapRegions.Rebuild(many, true));
        Equal((short)1, many.Regions[63].Land);
        Equal(3, many.Tiles.ToArray().Count(t => !t.Ocean && t.Region == 63));
        Equal(63, many.Regions.ToArray().Sum(r => r.Land));

        // ID 1 is freed only by small-ocean aggregation. ID 64 remaps into it only above 16 tiles.
        foreach (int size in new[] { 16, 17 })
        {
            var seas = GenerationWorld(160, 130, true).Map;
            seas.Tiles.Fill(new Tile { Terrain = Terrain.Plains });
            for (int id = 1; id <= 64; ++id)
            {
                int x = 4 + (id - 1) % 8 * 18, y = 4 + (id - 1) / 8 * 16;
                int count = id == 1 ? 1 : id == 64 ? size : 9;
                for (int tile = 0; tile < count; ++tile)
                    seas.TileAt(new(x + tile % 4 * 2, y + tile / 4 * 2)).Terrain = Terrain.Ocean;
            }
            Equal((false, size == 16), MapRegions.Rebuild(seas, true));
            Equal((byte)(size == 16 ? 63 : 1), seas.TileAt(new(130, 116)).Region);
            Equal((short)(size == 16 ? 0 : 17), seas.Regions[1].Water);
            Equal((short)10, seas.Regions[63].Water); // ID 63's own nine tiles plus the small first sea
        }
    }

    private static void GenerationRegionBorderSkips()
    {
        var map = GenerationWorld(20, 20, true).Map;
        map.Tiles.Fill(new Tile { Terrain = Terrain.Plains, Region = 1 });
        var point = new MapPoint(10, 10);
        map.TileAt(point).Region = 2;
        for (int d = 0; d < 8; ++d)
        {
            ref var tile = ref map.TileAt(MapNeighborhoods.Offset(map.Geometry, point, MapNeighborhoods.Directions[d], true));
            tile.Terrain = Terrain.Ocean;
            tile.Region = (byte)(10 + d);
        }
        MapRegions.RebuildBorders(map, true);
        Equal(new RegionSet().With(10).With(13).With(15).With(17), map.Regions[2].NeighboringWater);
    }

    // U03 uniform-cost bounded query: seed cost, strict limit, zero-valued cache hit, customized slot domain.
    private static void GenerationConnectivityCosts()
    {
        var map = GenerationWorld(80, 50, true).Map;
        map.Tiles.Fill(new Tile { Terrain = Terrain.Plains, Region = 1 });
        var rules = GenerationRules();
        var paths = new GenerationPathWorkspace();
        var source = new MapPoint(10, 10);
        Equal(4, GenerationConnectivity.PathLength(map, true, rules, paths, source, new(16, 10), false, 5));
        Equal(0, GenerationConnectivity.PathLength(map, true, rules, paths, source, new(16, 10), false, 5));
        Equal(-1, GenerationConnectivity.PathLength(map, true, rules, new(), source, new(18, 10), false, 5));
        rules.Units[2] = rules.Units[2] with { Domain = RuleDomain.Sea };
        Equal(-1, GenerationConnectivity.PathLength(map, true, rules, new(), source, new(16, 10), false, 5));
        rules.Units[2] = rules.Units[2] with { Domain = RuleDomain.Land, Abilities = new() { CoastBound = true } };
        map.TileAt(source).ResourceSuppressed = true;
        Equal(-1, GenerationConnectivity.PathLength(map, true, rules, new(), source, new(16, 10), false, 5));
        map.TileAt(source).ResourceSuppressed = false;
        var seam = new GenerationPathWorkspace();
        Equal(-1, GenerationConnectivity.PathLength(map, false, rules, seam, new(0, 10), new(76, 10), false, 5));
        Equal(3, seam.ResultCost); // flood wraps, but selection tests the original source window first
        GenerationConnectivity.Rebuild(map, true, rules, new());
        Equal(true, map.CoarseGrid(0).ToArray().Any(v => v != 0));
        Equal(true, map.CoarseGrid(1).ToArray().All(v => v == 0));
    }

    // W02 Fertility: floor/clamp vectors, signed custom yields, mining precedence, resource variant 2.
    private static void GenerationFertilityRules()
    {
        Equal(new OwnedList<byte>([8, 8, 9, 15, 15]),
            new(new[] { 0, 120, 136, 232, 1000 }.Select(MapGeneration.FertilityFromTotal)));
        var world = GenerationWorld(40, 20, true);
        world.Map.Tiles.Fill(new Tile { Terrain = Terrain.Plains, Region = 1 });
        var rules = new RuleTables();
        for (int i = 0; i < 22; ++i) rules.Terrain[i] = new() { Food = 1 };
        var both = new BaseTerrainWork(new(TerrainResult.ImproveInPlace), new(TerrainResult.ImproveInPlace));
        rules.Terrain[1] = rules.Terrain[1] with { Work = both };
        GenerationPhase(world).Fertility(rules);
        Equal((byte)8, world.Map.TileAt(new(10, 10)).Fertility); // 32 * (3 + mining 1) = 128
        rules.Terrain[1] = rules.Terrain[1] with { Work = both with { Mining = new() } };
        GenerationPhase(world).Fertility(rules);
        Equal((byte)10, world.Map.TileAt(new(10, 10)).Fertility); // 32 * (3 + irrigation 2) = 160
        for (int i = 0; i < 22; ++i) rules.Terrain[i] = new() { Food = 255, Shields = 255, Trade = 255 };
        GenerationPhase(world).Fertility(rules);
        Equal((byte)8, world.Map.TileAt(new(10, 10)).Fertility);
        Equal((short)400, world.Map.Regions[1].Fertile);
        Equal(true, world.Map.Tiles.ToArray().All(t => t.Owner is null));

        world.Map.Tiles.Fill(new Tile());
        var center = new MapPoint(10, 10);
        world.Map.TileAt(center).Terrain = Terrain.Plains;
        world.Map.PatternSeed = 1;
        while (!MapNeighborhoods.City.Take(8).Any(o => world.Map.SpecialResource(
            MapNeighborhoods.Offset(world.Map.Geometry, center, o, true)) == 2)) ++world.Map.PatternSeed;
        rules = new();
        rules.Terrain[21] = new() { Food = 127 }; // first special Ocean
        GenerationPhase(world).Fertility(rules);
        Equal((byte)15, world.Map.TileAt(center).Fertility);
        rules.Terrain[21] = new();
        rules.Terrain[32] = new() { Food = 127 }; // second special is deliberately unused
        GenerationPhase(world).Fertility(rules);
        Equal((byte)8, world.Map.TileAt(center).Fertility);
    }
}
