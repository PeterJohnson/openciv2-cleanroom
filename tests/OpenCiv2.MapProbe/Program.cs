using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

bool presentation = args.Length == 4 && args[3] is "--presentation-checks" or "--gpu-presentation-checks";
bool rendererOptions = args.Length == 4 && args[3] == "--renderer-options";
bool optimizationChecks = args.Length == 5 && args[3] == "--gpu-optimization-checks";
bool samplingChecks = args.Length == 5 && args[3] == "--gpu-sampling-checks";
bool matrix = args.Length == 5 && args[3] == "--matrix";
bool experiments = args.Length == 5 && args[3] == "--gpu-experiments";
bool uploadChecks = args.Length == 4 && args[3] == "--gpu-upload-check";
bool gpuMatrix = args.Length == 5 && args[3] is "--gpu-matrix" or "--gpu-experiment-matrix";
bool performance = args.Length is 4 or 5 && args[3] is "--performance" or "--diagnostic-performance" or "--diagnostic-direct-performance";
bool replay = args.Length == 5 && args[3] is "--replay-native-map" or "--replay-native-navigation" or "--replay-gpu-map" or "--replay-gpu-experiment";
bool panOverlap = args.Length == 5 && args[3] == "--diagnostic-pan-overlap";
bool pixels = args.Length == 4 && args[3] == "--atlas-pixels";
if (args.Length != 3 && !presentation && !optimizationChecks && !samplingChecks && !rendererOptions && !matrix && !experiments && !uploadChecks && !gpuMatrix && !performance && !replay && !panOverlap && !pixels)
    throw new ArgumentException("Usage: OpenCiv2.MapProbe ASSET_ROOT FONT_ROOT OUTPUT [--presentation-checks | --gpu-presentation-checks | --matrix ART_FIXTURES | --gpu-matrix ART_FIXTURES | --gpu-experiment-matrix ART_FIXTURES | --gpu-experiments RECEIPT | --gpu-upload-check | --performance [WORKLOAD] | --diagnostic-performance [WORKLOAD] | --diagnostic-direct-performance [WORKLOAD] | --replay-native-map RECEIPT | --replay-gpu-map RECEIPT | --replay-gpu-experiment RECEIPT | --diagnostic-pan-overlap RECEIPT | --atlas-pixels]");

Func<string, Stream> Root(string directory) => name => File.OpenRead(Path.Combine(directory, name));
var start = Stopwatch.GetTimestamp();
var atlas = PinnedGraphics.LoadAtlas(Root(args[0]));
double admissionMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
start = Stopwatch.GetTimestamp();
var fonts = PinnedGraphics.LoadFonts(Root(args[1]));
double fontsMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
if (presentation)
{
    using var context = args[3] == "--gpu-presentation-checks" ? new MapProbeGlContext() : null;
    MapPresentationChecks.Run(atlas, fonts, args[2], context?.Context);
    return;
}
if (optimizationChecks)
{
    using var gl = new MapProbeGlContext();
    GpuOptimizationChecks.Run(gl.Context, atlas, fonts, args[2], args[4], gl.Renderer, gl.Version);
    return;
}
if (samplingChecks)
{
    using var gl = new MapProbeGlContext();
    GpuSamplingChecks.Run(gl.Context, atlas, fonts, args[2], args[4]);
    return;
}
if (rendererOptions)
{
    RendererOptionChecks.Run(atlas, fonts, args[2]);
    return;
}
if (experiments)
{
    GpuMapExperiments.Run(atlas, fonts, args[4], args[2]);
    return;
}
if (uploadChecks)
{
    GpuUploadChecks.Run(atlas, fonts, args[2]);
    return;
}
if (performance)
{
    NativeMapPerformance.Run(atlas, fonts, args[2], args[3] != "--performance", admissionMilliseconds, fontsMilliseconds,
        args.Length == 5 ? args[4] : null, direct: args[3] == "--diagnostic-direct-performance");
    return;
}
if (replay)
{
    NativeMapNavigationReplay.Run(atlas, fonts, args[4], args[2], useGpu: args[3] is "--replay-gpu-map" or "--replay-gpu-experiment",
        gpuExperiment: args[3] == "--replay-gpu-experiment");
    return;
}
if (panOverlap)
{
    NativeMapPanOverlap.Run(atlas, fonts, args[4], args[2]);
    return;
}
Require(atlas.Sprites.Count == 2105 && fonts.Faces.Count == 12, "pinned catalog counts");
if (pixels)
{
    start = Stopwatch.GetTimestamp();
    var decoded = atlas.Sprites.ToDictionary(sprite => sprite.Key, sprite =>
        Convert.ToHexStringLower(SHA256.HashData(atlas.Decode(sprite.Key).Pixels)), StringComparer.Ordinal);
    var report = new
    {
        format = "openciv2-atlas-pixels/1",
        admissionMilliseconds,
        decodeAndHashMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds,
        identity = JsonDocument.Parse(ApplicationIdentity.Read(typeof(Program).Assembly)).RootElement,
        sprites = decoded
    };
    File.WriteAllText(args[2], JsonSerializer.Serialize(report) + Environment.NewLine);
    return;
}
if (matrix)
{
    NativeMapMatrix.Run(atlas, fonts, args[2], args[4]);
    return;
}
if (gpuMatrix)
{
    GpuMapMatrix.Run(atlas, fonts, args[2], args[4], experiment: args[3] == "--gpu-experiment-matrix");
    return;
}
start = Stopwatch.GetTimestamp();
long decodedPixels = 0;
foreach (var sprite in atlas.Sprites)
{
    var decoded = atlas.Decode(sprite.Key);
    Require(decoded.Width == sprite.PixelWidth && decoded.Height == sprite.PixelHeight, "catalog and decoded dimensions agree");
    decodedPixels += (long)decoded.Width * decoded.Height;
}
double decodeAllMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

RendererLibraryChecks.Run(atlas, fonts);
using var session = new SeamSession();
var authored = new MapScene(session.Player.ObserveMap()!);
GeometryChecks(authored);
start = Stopwatch.GetTimestamp();
using var painter = new MapPainter(atlas, fonts);
double textureMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
var (textures, textureBytes) = painter.TextureUsage;
ValidateFrames(painter, session, authored);
FeatureArtChecks(painter, authored.Map);
Console.WriteLine($"Validated {atlas.Sprites.Count} atlas bindings, {fonts.Faces.Count} faces, geometry and raster cache lifetimes.");

var scenarios = new List<object>
{
    Measure(session, painter, "authored", 1020, 720, 1.25, 1)
};
SeedStressWorld(session);
foreach (var (name, width, height, zoom, scaling) in new[]
{
    ("half-world-1080p", 1920, 1080, 0.31, 1.0),
    ("4k-standard", 3840, 2160, 1.0, 1.0),
    ("4k-zoomed-out", 3840, 2160, 0.25, 1.0),
    ("5k-zoomed-out-2x", 5120, 2880, 0.25, 2.0),
    ("5k-half-world", 5120, 2880, 0.83, 1.0)
})
    scenarios.Add(Measure(session, painter, name, width, height, zoom, scaling));
string rasterReport = JsonSerializer.Serialize(new
{
    format = "openciv2-map-raster/1",
    platform = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    scope = "Simulated physical-pixel workloads using the production Skia CPU raster renderer. No display, GPU, compositor or frame-pacing measurement.",
    identity = JsonDocument.Parse(ApplicationIdentity.Read(typeof(Program).Assembly)).RootElement,
    assets = new { admissionMilliseconds, fontsMilliseconds, decodeAllMilliseconds, decodedPixels, textures, textureBytes, textureMilliseconds },
    checks = new[] { "full pinned corpus", "flat and wrapped diamond edges", "object hit regions", "cached/dirty pixel equivalence",
        "scene and DPI invalidation", "obsolete scene epoch", "texture disposal", "unchanged gameplay and presentation RNG", "pinned feature art and observer-filtered joins" },
    scenarios
}, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(args[2], rasterReport + Environment.NewLine);

static void Require(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"Map check failed: {name}");
}

static void GeometryChecks(MapScene scene)
{
    var map = scene.Map;
    var flat = map with { Flat = true };
    var camera = new MapCamera(50, 40, 0.75);
    Require(camera.Hit(camera.ScreenX(-1), camera.ScreenY(1), flat) is null, "flat left");
    Require(camera.Hit(camera.ScreenX(-1), camera.ScreenY(1), map) == new MapPoint(map.Width - 1, 1), "western wrap on odd row");
    Require(camera.Hit(camera.ScreenX(map.Width), camera.ScreenY(0), map) == new MapPoint(0, 0), "eastern wrap on even row");
    Require(camera.Hit(camera.ScreenX(-2 * map.Width + 2), camera.ScreenY(2), map) == new MapPoint(2, 2), "multiple display copies resolve canonically");
    Require(camera.Hit(camera.ScreenX(0), camera.ScreenY(-1), map) == new MapPoint(0, 0), "closed north diamond tip");
    Require(camera.Hit(camera.ScreenX(0), camera.ScreenY(-1) - 0.1, map) is null, "north exterior");
    Require(camera.Hit(camera.ScreenX(0), camera.ScreenY(map.Height + 1), map) is null, "south exterior");
    Require(camera.Hit(camera.ScreenX(map.Width - 1) + 15, camera.ScreenY(1), flat) == new MapPoint(map.Width - 1, 1), "east edge inside diamond");
    Require(camera.Hit(camera.ScreenX(map.Width - 1) + 25, camera.ScreenY(1), flat) is null, "east edge outside diamond");
    var zoomed = camera.ZoomAt(camera.ScreenX(2), camera.ScreenY(2), 3);
    Require(zoomed.Hit(camera.ScreenX(2), camera.ScreenY(2), map) == new MapPoint(2, 2), "pointer anchored zoom");
    for (int y = 0; y < map.Height; y++)
        for (int x = y & 1; x < map.Width; x += 2)
            Require(camera.Hit(camera.ScreenX(x), camera.ScreenY(y), flat) == new MapPoint(x, y), "flat centre round trip");
    Require(scene.Pick(camera, camera.ScreenX(2), camera.ScreenY(2) - camera.Zoom * 17) == new MapPick(new(2, 2), MapPickKind.Unit), "unit overhang");
    Require(scene.Pick(camera, camera.ScreenX(6) - 6 * camera.Zoom, camera.ScreenY(4) - 5) == new MapPick(new(6, 4), MapPickKind.City), "city art");
}

// P02 known feature joins, W01 §3 directions, P03 §5 crop identities.
static void FeatureArtChecks(MapPainter painter, MapObservation authored)
{
    var road = new ObservedTile(2, MapFeatures.Road, true, 0, false);
    var known = new ObservedTile(2, default, false, 0, false);
    var tiles = Enumerable.Repeat(known, 16).ToArray();
    var point = new MapPoint(0, 2);
    tiles[8] = road;
    tiles[4] = road; // NE.
    tiles[11] = road; // West neighbor across the horizontal seam.
    tiles[15] = road; // SW neighbor across the seam.
    tiles[12] = road with { Terrain = null }; // Hidden SE supplies no art join.
    var map = authored with { Width = 8, Height = 4, Flat = false, Tiles = new(tiles), Units = new([]), Cities = new([]) };
    Require(new MapScene(map).Connections(point, road) == new TileConnections(5, 49), "wrapped river and road joins");
    Require(new MapScene(map with { Flat = true }).Connections(point, road) == new TileConnections(1, 1), "flat river and road joins");
    Require(MapScene.Route(road with { Improvements = MapFeatures.Road | MapFeatures.Railroad }) ==
        MapFeatures.Railroad, "rail takes precedence over the road art");
    Require(MapArtCatalog.Stock.Terrain[0].FirstResource == "resource/0" &&
        MapArtCatalog.Stock.Terrain[10].SecondResource == "resource/21", "pinned resource variants");

    using var surface = SKSurface.Create(new SKImageInfo(80, 60));
    var camera = new MapCamera(40, 30, 1);
    string Render(ObservedTile tile, int batches)
    {
        long epoch = painter.SetScene(new(map with { Width = 2, Height = 1, Flat = true, Tiles = new([tile]) }));
        var stats = painter.Draw(surface.Canvas, epoch, camera, 80, 60, 1, null);
        Require(stats.Batches == batches, "feature image added to its own atlas batch");
        return Hash(surface);
    }
    string grass = Render(known, 1);
    Require(grass != Render(known with { GrasslandShield = true }, 2), "grassland shield sprite contributes pixels");
    Require(grass != Render(known with { River = true }, 2), "river sprite contributes pixels");
    Require(grass != Render(known with { Improvements = MapFeatures.Road }, 2), "road sprite contributes pixels");
    Require(grass != Render(known with { Improvements = MapFeatures.Railroad }, 2), "rail sprite contributes pixels");
    var desert = known with { Terrain = 0 };
    string desertBase = Render(desert, 1);
    var resource = Render(desert with { Resource = 1 }, 2);
    Require(desertBase != resource && resource != Render(desert with { Resource = 2 }, 2), "different resource variants");
}

static void ValidateFrames(MapPainter painter, SeamSession session, MapScene scene)
{
    var game = session.Runtime.GameplayRandom.Capture();
    var presentation = session.Runtime.PresentationRandom.Capture();
    var before = session.Runtime.World.Map.Tiles.ToArray();
    var units = session.Runtime.World.Entities.Units.Scanned.ToArray();
    var cities = session.Runtime.World.Entities.Cities.Scanned.ToArray();
    using var output = SKSurface.Create(new SKImageInfo(800, 600));
    using var expected = SKSurface.Create(new SKImageInfo(800, 600));
    var camera = MapCamera.Initial;
    long epoch = painter.SetScene(scene);
    MapDrawStats Draw(SKSurface target, MapPick? pick, double scaling = 1, long? version = null) =>
        painter.Draw(target.Canvas, version ?? epoch, camera, 400, 300, scaling, pick);
    var unit = new MapPick(new(2, 2), MapPickKind.Unit);
    var city = new MapPick(new(6, 4), MapPickKind.City);
    Require(Draw(output, unit, 2).Redraw == MapRedraw.Full, "initial render");
    string selected = Hash(output);
    Require(Draw(output, unit, 2).Redraw == MapRedraw.Cached && Hash(output) == selected, "cached pixel equivalence");
    Require(Draw(output, city, 2).Redraw == MapRedraw.Dirty && Hash(output) != selected, "dirty pixel change");
    painter.InvalidateFrame();
    Require(Draw(expected, city, 2).Redraw == MapRedraw.Full && Hash(output) == Hash(expected), "dirty equals clean full redraw");
    Require(Draw(output, city).Redraw == MapRedraw.Full, "DPI cache separation");

    var other = session.Runtime.Bind(session.Runtime.AddParticipant(new(2)), ControllerKind.Frontend);
    long next = painter.SetScene(new(other.ObserveMap()!));
    Require(Draw(output, city, version: epoch).Tiles == 0, "previous observer draw suppressed");
    var draw = Draw(output, null, version: next);
    Require(draw.Redraw == MapRedraw.Full && draw.Tiles > 0 && draw.PreparedChunks > 0, "fresh observer viewport");
    using (var empty = new MapTextures(null))
    {
        _ = empty.Get("terrain/2");
        Require(empty.Count == 1 && empty.PixelBytes > 0, "owned fallback images");
        empty.Dispose();
        empty.Dispose();
        Require(empty.Count == 0 && empty.PixelBytes == 0, "texture release");
        try { _ = empty.Get("terrain/2"); throw new InvalidOperationException("disposed textures remained accessible"); }
        catch (ObjectDisposedException) { }
    }
    Require(game == session.Runtime.GameplayRandom.Capture() && presentation == session.Runtime.PresentationRandom.Capture(), "unchanged RNG");
    Require(session.Runtime.World.Map.Tiles.SequenceEqual(before) &&
        session.Runtime.World.Entities.Units.Scanned.SequenceEqual(units) &&
        session.Runtime.World.Entities.Cities.Scanned.SequenceEqual(cities), "unchanged world after drawing");
}

static string Hash(SKSurface target)
{
    using var pixels = target.PeekPixels();
    return Convert.ToHexString(SHA256.HashData(pixels.GetPixelSpan()));
}

static void SeedStressWorld(SeamSession session)
{
    var world = session.Runtime.World;
    var geometry = new Geometry(384, 124);
    world.Map.Allocate(geometry);
    var memory = world.Map.RememberedLayer(1);
    for (int i = 0; i < geometry.Area; i++)
    {
        var point = geometry.Coordinates(i);
        world.Map.Tiles[i] = new Tile
        {
            Terrain = (Terrain)((i + point.Y) % 11),
            RevealedTo = new CivilizationSet().With(1, point.X % 13 != 0),
            River = i % 17 == 0,
            Owner = null
        };
        memory[i] = i % 17 == 0 ? MapFeatures.Road : i % 23 == 0 ? MapFeatures.Irrigation : MapFeatures.None;
    }
    world.Entities.Units.SetEnd(1600);
    for (int i = 0; i < 1600; i++)
    {
        var point = geometry.Coordinates(i * 13);
        world.Entities.Units.Storage[i] = new()
        {
            Token = new((uint)i + 100),
            Owner = new((byte)(i % 4 + 1)),
            Position = point,
            Type = new((byte)(i % 6)),
            Visibility = new CivilizationSet().With(1, i % 3 == 0)
        };
    }
    world.Entities.Cities.SetEnd(180);
    for (int i = 0; i < 180; i++)
    {
        var point = geometry.Coordinates(i * 103);
        world.Entities.Cities.Storage[i] = new()
        {
            Token = new((uint)i + 4000),
            Owner = new(1),
            Position = point,
            Name = "Riverbend",
            Size = (sbyte)(1 + i % 12)
        };
    }
    session.Runtime.Publish();
}

static object Measure(SeamSession session, MapPainter painter, string name, int width, int height, double zoom, double scaling)
{
    const int repeats = 16;
    var projection = new List<double>();
    var preparation = new List<double>();
    MapScene? scene = null;
    for (int i = 0; i <= repeats; i++)
    {
        long start = Stopwatch.GetTimestamp();
        var map = session.Player.ObserveMap()!;
        if (i != 0) projection.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        start = Stopwatch.GetTimestamp();
        scene = new(map);
        if (i != 0) preparation.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
    long epoch = painter.SetScene(scene);
    using var target = SKSurface.Create(new SKImageInfo(width, height));
    target.Canvas.Scale((float)scaling);
    var camera = new MapCamera(18, 17, zoom);
    var full = new List<double>();
    var cached = new List<double>();
    var dirty = new List<double>();
    MapDrawStats count = default;
    for (int i = 0; i <= repeats; i++)
    {
        var first = new MapPick(new(2, 2), MapPickKind.Tile);
        var second = new MapPick(new(4, 2), MapPickKind.Tile);
        painter.InvalidateFrame();
        count = Draw(first, MapRedraw.Full, full);
        Draw(first, MapRedraw.Cached, cached);
        Draw(second, MapRedraw.Dirty, dirty);

        MapDrawStats Draw(MapPick pick, MapRedraw kind, List<double> samples)
        {
            long start = Stopwatch.GetTimestamp();
            var outcome = painter.Draw(target.Canvas, epoch, camera, width / scaling, height / scaling, scaling, pick);
            target.Canvas.Flush();
            if (i != 0) samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            Require(outcome.Redraw == kind, $"{name} {kind}");
            return outcome;
        }
    }
    var visible = camera.Visible(scene!.Map, width / scaling, height / scaling).Select(tile => tile.Point).Distinct().Count();
    Console.WriteLine($"{name}: {width}x{height}, {count.Tiles} tile instances, {count.Objects} objects, {count.Batches} texture batches");
    return new
    {
        name,
        width,
        height,
        zoom,
        scaling,
        worldTiles = scene.Map.Tiles.Count,
        worldObservedUnits = scene.Map.Units.Count,
        worldObservedCities = scene.Map.Cities.Count,
        distinctPreparedTiles = visible,
        tileInstances = count.Tiles,
        visibleObjects = count.Objects,
        textureBatches = count.Batches,
        modelProjection = Summary(projection),
        modelPreparation = Summary(preparation),
        full = Summary(full),
        cached = Summary(cached),
        dirty = Summary(dirty)
    };
}

static object Summary(List<double> samples)
{
    samples.Sort();
    return new { medianMs = (samples[7] + samples[8]) / 2, p95Ms = samples[15], samplesMs = samples };
}
