using System.Security.Cryptography;
using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using SkiaSharp;

internal static class NativeMapNavigationReplay
{
    private sealed record ReferenceKey(string? Mode, int Width, int Height, double Density, ulong InitialRevision,
        ulong Revision, bool Flat, MapPoint? Update, MapCamera Camera, MapPick? Selection, MapDisplay Display, string Presentation);

    public static void Run(AssetAtlas atlas, PinnedFonts fonts, string input, string output, bool useGpu = false, bool gpuExperiment = false)
    {
        using var receipt = JsonDocument.Parse(File.ReadAllBytes(input));
        if (receipt.RootElement.GetProperty("format").GetString() != "openciv2-native-map-input-timing/1")
            throw new InvalidDataException("Expected a Linux map input timing receipt");
        if (receipt.RootElement.TryGetProperty("composition", out var composition) && composition.GetString() == "gpu" && !useGpu)
            throw new InvalidDataException("GPU receipts require --replay-gpu-map, not a CPU-only replay");
        using var gl = useGpu ? new MapProbeGlContext() : null;
        bool everyGpuFrame = Environment.GetEnvironmentVariable("OPENCIV2_REPLAY_ALL") == "1";
        var board = MapGallery.Open("Mixed");
        var results = new List<object>();
        // The board, admitted art and fonts are fixed for this Run. This key also
        // includes every input to its deterministic mutation replay and drawing.
        // Repeated states may reuse an independently fresh reference hash; every
        // retained frame is still read back and compared, without retaining pixels.
        var freshHashes = new Dictionary<ReferenceKey, string>();
        foreach (var series in receipt.RootElement.GetProperty("results").EnumerateArray())
        {
            string? mode = series.GetProperty("mode").GetString();
            var measured = series.GetProperty("samples");
            bool savedWarmup = series.TryGetProperty("warmupSamples", out var warmupSamples) && warmupSamples.GetArrayLength() > 0;
            var samples = (savedWarmup ? warmupSamples.EnumerateArray().Concat(measured.EnumerateArray()) : measured.EnumerateArray()).ToArray();
            bool dirty = mode == "dirty-world";
            bool recovery = mode == "resource-recovery";
            bool cold = mode == "cold-first-frame";
            // Early timing receipts only carry the camera for navigation.
            if (mode is not ("pan" or "continuous-zoom" or "dirty-world" or "resource-recovery" or "cold-first-frame") ||
                dirty && (samples.Length == 0 || !samples[0].TryGetProperty("updateX", out _))) continue;
            int width = series.GetProperty("width").GetInt32(), height = series.GetProperty("height").GetInt32();
            double density = series.GetProperty("density").GetDouble();
            int run = series.GetProperty("run").GetInt32();
            using var candidate = new MapPainter(atlas, fonts);
            using var reference = new MapPainter(atlas, fonts, parallelForeground: false, packGroundTextures: false);
            using var actual = useGpu ? SKSurface.Create(gl!.Context, true, new SKImageInfo(width, height)) :
                SKSurface.Create(new SKImageInfo(width, height));
            using var expected = SKSurface.Create(new SKImageInfo(width, height));
            actual.Canvas.Scale((float)density);
            expected.Canvas.Scale((float)density);
            var world = board.Map with
            {
                Flat = series.TryGetProperty("flat", out var flat) && flat.GetBoolean(),
                Revision = series.TryGetProperty("initialRevision", out var revisionStart) ? revisionStart.GetUInt64() : 0
            };
            var scene = new MapScene(world);
            var display = cold ? board.Display with { ReducedMotion = true } : board.Display;
            long bound = candidate.SetScene(scene), fresh = reference.SetScene(scene);
            var previous = Camera(cold ? samples[0] : series.TryGetProperty("initialView", out var initial) ? initial : series.GetProperty("halfWorldReceipt"));
            MapPoint? update = null;
            if (dirty)
            {
                int warmup = (series.TryGetProperty("warmup", out var count) ? count : receipt.RootElement.GetProperty("warmup")).GetInt32();
                update = new(samples[0].GetProperty("updateX").GetInt32(), samples[0].GetProperty("updateY").GetInt32());
                if (previous.Hit(width / density * .48, height / density * .52, world) != update)
                    throw new InvalidDataException("Dirty replay update does not match the viewport focus");
                if (!savedWarmup)
                    for (int i = 0; i < warmup; i++) world = Change(world, update);
                bound = candidate.SetScene(new(world));
            }
            if (!cold) Paint(candidate, actual, bound, previous);
            int verified = 0, bandedGround = 0, effects = 0, freshReferenceRenders = 0, freshReferenceHashHits = 0;
            var gpuComparisons = new List<object>();
            var redraws = new Dictionary<MapRedraw, int>();
            long damagedPixels = 0, groundPixels = 0;
            foreach (var row in samples)
            {
                var camera = Camera(row);
                ulong revision = row.GetProperty("revision").GetUInt64();
                int visibleEffects = row.GetProperty("effects").GetInt32();
                if ((camera == previous) != (dirty || recovery || cold) || row.GetProperty("scaling").GetDouble() != density ||
                    Math.Ceiling(row.GetProperty("width").GetDouble() * density) != width ||
                    Math.Ceiling(row.GetProperty("height").GetDouble() * density) != height ||
                    revision != world.Revision + (dirty ? 1UL : 0UL) || (visibleEffects > 0) != dirty)
                    throw new InvalidDataException($"Unexpected observation or camera for {width}×{height} {mode} run {run} frame {verified}");
                previous = camera;
                VisualFrame? visual = row.TryGetProperty("presentation", out var presentation) && presentation.ValueKind == JsonValueKind.Object
                    ? Presentation(row, update, visibleEffects) : null;
                if (update is { } point)
                {
                    if (point.X != row.GetProperty("updateX").GetInt32() || point.Y != row.GetProperty("updateY").GetInt32())
                        throw new InvalidDataException("Dirty replay moved its viewport focus");
                    world = Change(world, point);
                    scene = new(world);
                    bound = candidate.SetScene(scene);
                    reference.SetScene(null);
                    fresh = reference.SetScene(scene);
                }
                else if (recovery || cold)
                {
                    candidate.RecoverResources();
                    reference.RecoverResources();
                }
                else reference.InvalidateFrame();
                MapPick? selection = row.TryGetProperty("selection", out var selected)
                    ? new(new(selected.GetProperty("x").GetInt32(), selected.GetProperty("y").GetInt32()),
                        Enum.Parse<MapPickKind>(selected.GetProperty("kind").GetString()!)) : null;
                var drawn = Paint(candidate, actual, bound, camera, visual, selection);
                if (useGpu)
                {
                    gl!.Context.Flush(submit: true, synchronous: true);
                    NativeMapMatrix.Require(drawn.Error is null && drawn.Effects == visibleEffects &&
                        drawn.Tiles == row.GetProperty("tiles").GetInt32() && drawn.Objects == row.GetProperty("objects").GetInt32() &&
                        drawn.Labels == row.GetProperty("labels").GetInt32(), $"GPU replay observations frame {verified}");
                    bool crossCheck = verified == 0 || (verified + 1) % 30 == 0 || verified == samples.Length - 1;
                    if (everyGpuFrame || crossCheck)
                    {
                        using var read = GpuMapMatrix.Read(actual);
                        string actualHash = Convert.ToHexStringLower(SHA256.HashData(read.GetPixelSpan()));
                        var referenceKey = new ReferenceKey(mode, width, height, density,
                            series.TryGetProperty("initialRevision", out var initialRevision) ? initialRevision.GetUInt64() : 0,
                            revision, world.Flat, update, camera, selection, display,
                            row.TryGetProperty("presentation", out var recordedPresentation) ? recordedPresentation.GetRawText() : "");
                        string? expectedHash = null;
                        bool reused = everyGpuFrame && freshHashes.TryGetValue(referenceKey, out expectedHash);
                        var freshDiff = new GpuMapMatrix.Difference((long)width * height, 0, 0, 0, 0);
                        if (!reused || actualHash != expectedHash)
                        {
                            using var gpuFresh = SKSurface.Create(gl.Context, true, new SKImageInfo(width, height));
                            gpuFresh.Canvas.Scale((float)density);
                            using var freshPainter = new MapPainter(atlas, fonts);
                            long freshBinding = freshPainter.SetScene(new(world));
                            MapGpuComposition.Draw(freshPainter, gl.Context, gpuFresh.Canvas, freshBinding, camera,
                                width / density, height / density, density, selection, display, visual);
                            using var freshRead = GpuMapMatrix.Read(gpuFresh);
                            freshReferenceRenders++;
                            freshDiff = GpuMapMatrix.Compare(read.GetPixelSpan(), freshRead.GetPixelSpan());
                            string renderedHash = Convert.ToHexStringLower(SHA256.HashData(freshRead.GetPixelSpan()));
                            bool referenceChanged = reused && expectedHash != renderedHash;
                            expectedHash = renderedHash;
                            freshHashes[referenceKey] = renderedHash;
                            if (referenceChanged || actualHash != expectedHash)
                            {
                                string prefix = Path.Combine(Path.GetDirectoryName(output)!, $"failed-{width}-{height}-{density}-{mode}-{run}-{verified}");
                                NativeMapMatrix.Encode(actual, prefix + "-retained.png");
                                NativeMapMatrix.Encode(gpuFresh, prefix + "-fresh.png");
                                File.WriteAllText(prefix + ".json", JsonSerializer.Serialize(new { referenceKey, referenceChanged, freshDiff }));
                            }
                            NativeMapMatrix.Require(!referenceChanged, $"fresh reference changed for identical replay inputs at {mode}/{run}/{verified}");
                        }
                        else freshReferenceHashHits++;
                        bool exact = actualHash == expectedHash;
                        if (!gpuExperiment || everyGpuFrame) NativeMapMatrix.Require(exact, $"GPU replay fresh pixels {width}×{height} {mode} run {run} frame {verified}");
                        GpuMapMatrix.Difference? cpuDifference = null;
                        if (crossCheck)
                        {
                            Paint(reference, expected, fresh, camera, visual, selection);
                            using var cpuPixels = expected.PeekPixels();
                            cpuDifference = GpuMapMatrix.Compare(read.GetPixelSpan(), cpuPixels.GetPixelSpan());
                            if (verified == 0)
                            {
                                string prefix = Path.Combine(Path.GetDirectoryName(output)!, $"{width}-{mode}-{run}");
                                NativeMapMatrix.Encode(actual, prefix + "-gpu.png");
                                NativeMapMatrix.Encode(expected, prefix + "-cpu.png");
                            }
                        }
                        gpuComparisons.Add(new
                        {
                            frame = verified,
                            diff = cpuDifference,
                            freshGpuDiff = freshDiff,
                            freshGpuExact = exact,
                            freshReferenceHashReused = reused && exact,
                            freshReferenceSha256 = expectedHash
                        });
                    }
                    verified++;
                    continue;
                }
                var serial = Paint(reference, expected, fresh, camera, visual, selection);
                using var pixels = actual.PeekPixels();
                using var baseline = expected.PeekPixels();
                var captured = cold ? row.GetProperty("windowPixels") : default;
                if (drawn.Error is not null || serial.Error is not null || drawn.Effects != visibleEffects ||
                    ((recovery || cold) && (drawn.Redraw != MapRedraw.Full || drawn.PreparedChunks != row.GetProperty("preparedChunks").GetInt32())) ||
                    drawn.Tiles != row.GetProperty("tiles").GetInt32() ||
                    drawn.Objects != row.GetProperty("objects").GetInt32() || drawn.Labels != row.GetProperty("labels").GetInt32() ||
                    !pixels.GetPixelSpan().SequenceEqual(baseline.GetPixelSpan()) ||
                    (cold && RgbDigest(baseline, captured) != captured.GetProperty("rgbSha256").GetString()))
                {
                    NativeMapMatrix.Encode(actual, Path.ChangeExtension(output, "actual.png"));
                    NativeMapMatrix.Encode(expected, Path.ChangeExtension(output, "expected.png"));
                    throw new InvalidOperationException($"Native camera differs from the fresh map at {width}×{height} {mode} run {run} frame {verified}");
                }
                verified++;
                effects += visibleEffects;
                if (drawn.GroundReplayBands > 0) bandedGround++;
                redraws[drawn.Redraw] = redraws.GetValueOrDefault(drawn.Redraw) + 1;
                damagedPixels += drawn.DamagePixels;
                groundPixels += drawn.GroundDamagePixels;
            }
            NativeMapMatrix.Require(verified > 0, "native timing series includes frames");
            string? detail = cold ? series.GetProperty("detail").GetString() : null;
            Console.WriteLine($"{width}×{height} {density}× {mode} {detail} run {run}: {verified} observation frames; " +
                (useGpu ? $"{gpuComparisons.Count} fresh GPU comparisons (differences recorded; strict={!gpuExperiment || everyGpuFrame})" : "all CPU pixels equal"));
            results.Add(new
            {
                width,
                height,
                density,
                run,
                mode,
                detail,
                verified,
                gpuComparisons,
                freshReferenceRenders,
                freshReferenceHashHits,
                savedWarmupFrames = savedWarmup ? warmupSamples.GetArrayLength() : 0,
                nativeReadbacks = cold ? verified : 0,
                bandedGround,
                effects,
                redraws,
                damagePixels = damagedPixels,
                groundDamagePixels = groundPixels
            });

            MapObservation Change(MapObservation before, MapPoint? point)
            {
                var after = MapGallery.Changed(before, point);
                return MapGpuComposition.Features.HasFlag(MapGpuFeatures.Sparse) ? MapObservationDelta.Attach(before, after) : after;
            }

            MapDrawStats Paint(MapPainter painter, SKSurface surface, long epoch, MapCamera camera,
                VisualFrame? visual = null, MapPick? selection = null) =>
                useGpu && ReferenceEquals(painter, candidate)
                    ? MapGpuComposition.Draw(painter, gl!.Context, surface.Canvas, epoch, camera, width / density, height / density, density, selection, display, visual)
                    : painter.Draw(surface.Canvas, epoch, camera, width / density, height / density, density, selection, display, visual);
        }

        NativeMapMatrix.Require(results.Count > 0, "native receipt includes replayable first-frame, navigation, world-change or recovery series");
        var report = new
        {
            format = "openciv2-native-map-navigation-replay/1",
            gpuExperiment,
            everyGpuFrame,
            recordedNavigationPulse = true,
            scope = useGpu ? (everyGpuFrame
                ? "Every recorded observation and saved warmup read back and strictly compared with an independently fresh GPU reference. Identical replay inputs may reuse that reference hash. CPU/GPU differences sampled separately. No native-window pixel acceptance."
                : "Every recorded observation replayed on hardware GPU. First, every thirtieth and last frame compared with a fresh GPU render; sampled equality and CPU/GPU differences reported separately. Experiment mode records rejected differences; strict mode fails. No native-window pixel acceptance.") :
                "Skia world pixels at recorded X11 camera states and authored world revisions. Cold repeats also compare X11 map pixels outside the overview.",
            gpuRenderer = gl?.Renderer,
            inputSha256 = NativeMapMatrix.FileHash(input),
            identity = JsonDocument.Parse(ApplicationIdentity.Read(typeof(NativeMapNavigationReplay).Assembly)).RootElement,
            results
        };
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }

    private static MapCamera Camera(JsonElement row) => new(row.GetProperty("x").GetDouble(),
        row.GetProperty("y").GetDouble(), row.GetProperty("zoom").GetDouble());

    private static string RgbDigest(SKPixmap image, JsonElement capture)
    {
        int cutX = capture.GetProperty("cutX").GetInt32(), cutY = capture.GetProperty("cutY").GetInt32();
        if (cutX <= 0 || cutY <= 0 || cutX >= image.Width || cutY >= image.Height ||
            image.ColorType is not (SKColorType.Bgra8888 or SKColorType.Rgba8888))
            throw new InvalidDataException("Invalid native RGB map readback dimensions or pixel format");
        int red = image.ColorType == SKColorType.Bgra8888 ? 2 : 0, blue = 2 - red;
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var row = new byte[image.Width * 3];
        var pixels = image.GetPixelSpan();
        for (int y = 0; y < image.Height; y++)
        {
            int width = y < cutY ? image.Width : cutX;
            var source = pixels.Slice(y * image.RowBytes, width * 4);
            for (int x = 0; x < width; x++)
            {
                row[x * 3] = source[x * 4 + red];
                row[x * 3 + 1] = source[x * 4 + 1];
                row[x * 3 + 2] = source[x * 4 + blue];
            }
            digest.AppendData(row.AsSpan(0, width * 3));
        }
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }

    internal static VisualFrame Presentation(JsonElement row, MapPoint? point, int count)
    {
        var visual = row.GetProperty("presentation");
        var explosions = new List<EffectFrame>(count);
        foreach (var effect in visual.GetProperty("explosions").EnumerateArray())
        {
            int x = effect.GetProperty("x").GetInt32(), y = effect.GetProperty("y").GetInt32();
            double progress = effect.GetProperty("progress").GetDouble();
            var position = new MapPoint(x, y);
            if (point is null || position != point || progress is < 0 or >= 1)
                throw new InvalidDataException("Dirty replay received an unexpected effect position or phase");
            explosions.Add(new(new(effect.GetProperty("id").GetUInt64(), MapEffectKind.Explosion, position, position, 250),
                progress, effect.GetProperty("wrappedX").GetInt32()));
        }
        if (explosions.Count != count) throw new InvalidDataException("Dirty replay needs every visible effect phase");
        return new(new(explosions), new([]), new([]), visual.GetProperty("pulse").GetBoolean());
    }
}
