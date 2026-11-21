using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>Retained-map adapter for additional terrain markers. Formats owns their encoding.
/// Implementations only inspect/clear marker metadata and copy it to the fallback, without gameplay effects.</summary>
public interface IGenerationTerrainMarkers
{
    bool HasMarkers(int index);
    void Clear(int index);
    void CopyFirstTileToFallback();
}

/// <summary>W02 complete terrain generation and common finalization. Civilization placement is F05.
/// Inputs and rules must stay stable during this synchronous operation. Failure retains completed edits
/// and consumed choices; callers must not publish a failed generation as a completed world.</summary>
public static class MapGenerator
{
    public static void Generate(
        GameState world,
        ref StartingPositions starts,
        RuleTables rules,
        GameplayRandom random,
        string call,
        uint ticks,
        bool retainMap = false,
        bool wideLayout = false,
        GenerationPathWorkspace? paths = null,
        IGenerationTerrainMarkers? markers = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentException.ThrowIfNullOrEmpty(call);
        var map = world.Map;
        bool flat = world.Globals.Rules.FlatWorld;
        if (!retainMap)
        {
            var shape = new Geometry(world.WorldSetup.Width, world.WorldSetup.Height);
            // F05's supported presets/custom-size domain. Reject before allocating or drawing.
            if (shape.Width / 2 is < 20 or > 250 || shape.Height is < 20 or > 250
                || shape.Area is < 1000 or > 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(world), "Unsupported generation dimensions");
            }
            for (int i = 0; i < 5; ++i)
            {
                if (world.WorldSetup.Parameters[i] is < -1 or > 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(world), "World parameters must be -1, 0 or 1");
                }
            }
            starts = new();
            map.PatternSeed = Arithmetic.MapPatternSeedFromTicks(ticks);
            if (flat)
            {
                world.WorldSetup.Parameters[2] *= 2;
                world.WorldSetup.Parameters[3] *= 2;
            }
            map.Allocate(shape);
            map.StoredTopology = flat ? (ushort)0x8000 : (ushort)0;
            // New spatial allocation has no imported markers or incoming flood.
            markers = null;
            paths ??= new();
            paths.Reset();
        }
        else
        {
            foreach (var tile in map.Tiles)
            {
                if (tile.Terrain > Terrain.Ocean)
                {
                    throw new ArgumentException("Retained map contains an invalid terrain kind", nameof(world));
                }
            }
        }
        var generation = new MapGeneration(map, flat, world.WorldSetup, random, call, markers);
        if (!retainMap)
        {
            generation.CreateLand();
            generation.InitialTerrain();
            generation.Rainfall();
            generation.Weathering();
            generation.Voting();
            generation.Rivers();
        }
        generation.Finalize(rules, paths ?? new(), retainMap, wideLayout);
    }
}

/// <summary>W02 invocation-owned scratch and phase dependencies. No synthetic pump/phase callbacks.</summary>
internal sealed partial class MapGeneration
{
    private readonly MapStorage map;
    private readonly Geometry shape;
    private readonly bool flat;
    private readonly int landMass, form, temperature, climate, age;
    private readonly GameplayRandom random;
    private readonly string call;
    private readonly IGenerationTerrainMarkers? markers;
    private readonly Dictionary<string, int> choices = [];
    internal byte[] Coverage { get; }
    private readonly bool[] marked;
    private int minY, maxY;
    internal int Placed { get; private set; }

    internal MapGeneration(
        MapStorage map,
        bool flat,
        WorldSetup setup,
        GameplayRandom random,
        string call,
        IGenerationTerrainMarkers? markers = null)
    {
        this.map = map;
        shape = map.Geometry;
        this.flat = flat;
        landMass = setup.Parameters[0];
        form = setup.Parameters[1];
        temperature = setup.Parameters[2];
        climate = setup.Parameters[3];
        age = setup.Parameters[4];
        this.random = random;
        this.call = call;
        this.markers = markers;
        maxY = shape.Height - 1;
        Coverage = new byte[shape.Area];
        marked = new bool[shape.Area];
    }

    private int Pick(string site, int n)
    {
        if (n <= 1)
        {
            return 0;
        }
        int attempt = choices.GetValueOrDefault(site);
        choices[site] = attempt + 1;
        return random.PrimaryModulo(new ChoiceKey("W02." + site, call, attempt), n);
    }

    private MapPoint Neighbor(MapPoint point, MapPoint offset) => MapNeighborhoods.Offset(shape, point, offset, flat);
    private static bool Plain(Tile tile, Terrain kind) =>
        tile.Terrain == kind && !tile.River && !tile.ResourceSuppressed;

    private void Replace(int index, Terrain terrain)
    {
        ref var tile = ref map.Tiles[index];
        tile.Terrain = terrain;
        tile.River = false;
        tile.ResourceSuppressed = false;
        markers?.Clear(index);
    }

    private void Replace(MapPoint point, Terrain terrain) => Replace(shape.TileIndex(point)!.Value, terrain);
}
