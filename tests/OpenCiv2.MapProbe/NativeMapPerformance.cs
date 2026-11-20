using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static class NativeMapPerformance
{
    private sealed record Sample(double FrameMs, double ProjectMs, double PrepareMs, double DrawMs, double GcPauseMs,
        long AllocatedBytes, int PreparedChunks, int Chunks, int Tiles, int Objects, int Labels, int Batches, long DamagePixels,
        double GroundTier, string Redraw, long GroundDamagePixels, double GroundBuildMs, double GroundSelectorMs, double TexturePrepareMs,
        int ParallelGroundChunks, long GroundCacheBytes, long GroundCacheLimitBytes,
        double TerrainMs, double ForegroundMs, double LabelLayoutMs, double ViewportBlitMs, int ForegroundBands, int PreparedBodyTextures,
        int OverlappedBodyTextures, int GroundReplayBands, int AlignedGroundChunks, long TextureCacheBytes,
        long DemandRasterLoads, double TintPrepareMs, double BandSetupMs, double BandRecordMs, double BandReplayMs, double BandRepairMs);
    private readonly record struct Workload(int Width, int Height, double Density);
    private static readonly string[] Modes = ["cold-full", "unchanged", "pan", "continuous-zoom", "dirty-world", "replacement"];

    public static void Run(AssetAtlas atlas, PinnedFonts fonts, string output, bool quick, double atlasAdmissionMs, double fontAdmissionMs,
        string? modeFilter, bool direct)
    {
        if (modeFilter is not null && !Modes.Contains(modeFilter, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown performance workload '{modeFilter}'. Expected one of {string.Join(", ", Modes)}.");
        string[] modes = modeFilter is null ? Modes : [modeFilter];
        using var fixture = new ScopedMapPerformanceWorld();
        using var process = Process.GetCurrentProcess();
        int runs = quick ? 1 : 3, continuous = quick ? 18 : 300, warmup = quick ? 3 : 30, replacements = quick ? 3 : 20;
        var results = new List<object>();
        foreach (var size in new[] { new Workload(1920, 1080, 1), new Workload(3840, 2160, 1), new Workload(5120, 2880, 2) })
        {
            for (int run = 1; run <= runs; run++)
            {
                foreach (string mode in modes)
                {
                    fixture.Restart();
                    bool cold = mode is "cold-full" or "replacement";
                    var result = Measure(atlas, fonts, fixture, size, run, mode, output, cold ? replacements : continuous,
                        cold ? 0 : warmup, quick, direct);
                    results.Add(result);
                    Write();
                }
            }
        }
        if (modeFilter is null || modeFilter == "replacement")
            Write(Replacements(atlas, fonts, fixture));

        void Write(object? lifetimeResult = null)
        {
            process.Refresh();
            var report = new
            {
                format = "openciv2-native-map-performance/1",
                scope = "Local Skia CPU offscreen. Host presentation, physical GPU and display pacing are separate evidence.",
                diagnostic = quick,
                directViewportFallback = direct,
                modes,
                platform = RuntimeInformation.OSDescription,
                runtime = RuntimeInformation.FrameworkDescription,
                hardware = new
                {
                    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    processors = Environment.ProcessorCount,
                    availableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                    cpu = OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo")
                        ? File.ReadLines("/proc/cpuinfo").FirstOrDefault(line => line.StartsWith("model name", StringComparison.Ordinal)) : null,
                    graphics = "Skia CPU raster"
                },
                identity = JsonDocument.Parse(ApplicationIdentity.Read(typeof(NativeMapPerformance).Assembly)).RootElement,
                contract = NativeMapMatrix.FileHash("docs/performance/map-rendering.md"),
                matrix = NativeMapMatrix.FileHash("docs/testing/map-rendering.md"),
                runCount = runs,
                warmup,
                continuous,
                replacements,
                atlasAdmissionMs,
                fontAdmissionMs,
                peakWorkingSetBytes = process.PeakWorkingSet64,
                lifetime = lifetimeResult,
                results
            };
            File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        }
    }

    private static object Measure(AssetAtlas atlas, PinnedFonts fonts, ScopedMapPerformanceWorld fixture, Workload size, int run,
        string mode, string output, int measured, int warm, bool quick, bool direct)
    {
        // Force only the existing viewport-allocation fallback. Ground chunks and
        // pinned texture selection use the same paths as the retained renderer.
        using var candidate = new MapPainter(atlas, fonts, allocateRaster: direct
            ? info => info.Width == size.Width && info.Height == size.Height ? null : SKSurface.Create(info)
            : null);
        using var reference = new MapPainter(atlas, fonts, parallelForeground: false, packGroundTextures: false);
        using var surface = SKSurface.Create(new SKImageInfo(size.Width, size.Height));
        using var expected = SKSurface.Create(new SKImageInfo(size.Width, size.Height));
        surface.Canvas.Scale((float)size.Density);
        expected.Canvas.Scale((float)size.Density);
        double width = size.Width / size.Density, height = size.Height / size.Density;
        double half = Math.Sqrt(width * height / (252 * 32d * 252 * 16 * .5));
        half = Math.Max(.25, half);
        double X = width / 2 - 64 * 32 * half, Y = height / 2 - 110 * 16 * half;
        var origin = new MapCamera(X, Y, half);
        var observation = fixture.Capture();
        var scene = new MapScene(observation);
        long epoch = candidate.SetScene(scene), fresh = reference.SetScene(new(observation));
        var samples = new List<Sample>(measured);
        int verified = 0;
        double minZoom = double.MaxValue, maxZoom = 0;
        MapCamera? lastCamera = null;
        for (int frame = -warm; frame < measured; frame++)
        {
            long time = Stopwatch.GetTimestamp(), beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
            var beforePause = GC.GetTotalPauseDuration();
            var camera = origin;
            if (mode == "pan")
            {
                int step = frame + warm;
                // Include one physical pixel, diagonal movement and discrete
                // changes in the sampling phase during the sustained drag.
                double phase = step % 29 == 17 ? .25 : 0;
                camera = origin.Pan(((step < 3 ? step : step * 3) + phase) / size.Density,
                    Math.Round(step * .625) / size.Density, observation, width, height);
            }
            else if (mode == "continuous-zoom")
            {
                double near = Math.Clamp(half * 1.8 + .25, .82, 1.45), distant = Math.Clamp(half * .54, .26, .33);
                double desired = distant + (near - distant) * (.5 + .5 * Math.Sin(frame * .047));
                camera = origin.ZoomAt(width * .57, height * .43, Math.Log(desired / half, 1.25)).Pan(0, 0, observation, width, height);
            }
            if (mode is "pan" or "continuous-zoom")
                NativeMapMatrix.Require(camera != lastCamera, $"{size.Width} {mode} frame {frame} must change its camera");
            lastCamera = camera;
            double project = 0, prepare = 0;
            VisualFrame? visual = null;
            if (mode is "cold-full" or "dirty-world" or "replacement")
            {
                if (mode == "replacement")
                {
                    candidate.RecoverResources();
                    fixture.Switch(frame);
                }
                else if (mode == "dirty-world") fixture.Change(frame + warm);
                else candidate.RecoverResources();
                long boundary = Stopwatch.GetTimestamp();
                observation = fixture.Capture();
                project = Stopwatch.GetElapsedTime(boundary).TotalMilliseconds;
                boundary = Stopwatch.GetTimestamp();
                scene = new(observation);
                epoch = candidate.SetScene(scene);
                prepare = Stopwatch.GetElapsedTime(boundary).TotalMilliseconds;
                if (mode == "dirty-world")
                {
                    var p = new MapPoint(62, 108);
                    var effect = new MapEffect(1, MapEffectKind.Explosion, p, p, 1000);
                    visual = new(new([new EffectFrame(effect, (frame + warm) % 8 / 8d, p.X)]), new([]), new([]), true);
                }
            }

            long drawStart = Stopwatch.GetTimestamp();
            var stats = candidate.Draw(surface.Canvas, epoch, camera, width, height, size.Density, null, visual: visual);
            surface.Canvas.Flush();
            double draw = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
            double total = Stopwatch.GetElapsedTime(time).TotalMilliseconds;
            if (frame < 0) continue;
            samples.Add(new(total, project, prepare, draw, (GC.GetTotalPauseDuration() - beforePause).TotalMilliseconds,
                GC.GetAllocatedBytesForCurrentThread() - beforeAllocation, stats.PreparedChunks, stats.GroundChunks,
                stats.Tiles, stats.Objects, stats.Labels, stats.Batches, stats.DamagePixels, stats.GroundTier, stats.Redraw.ToString(),
                stats.GroundDamagePixels, stats.GroundBuildMs, stats.GroundSelectorMs, stats.TexturePrepareMs, stats.ParallelGroundChunks,
                stats.GroundCacheBytes, stats.GroundCacheLimitBytes, stats.TerrainMs, stats.ForegroundMs, stats.LabelLayoutMs, stats.ViewportBlitMs,
                stats.ForegroundBands, stats.PreparedBodyTextures, stats.OverlappedBodyTextures, stats.GroundReplayBands,
                stats.AlignedGroundChunks, stats.TextureCacheBytes, stats.DemandRasterLoads, stats.TintPrepareMs,
                stats.BandSetupMs, stats.BandRecordMs, stats.BandReplayMs, stats.BandRepairMs));
            minZoom = Math.Min(minZoom, camera.Zoom);
            maxZoom = Math.Max(maxZoom, camera.Zoom);

            // Reference viewport is rebuilt independently. A changed world also drops
            // its reference ground entirely, so the candidate's damage logic cannot
            // reproduce the same invalidation failure on both surfaces.
            if (quick || mode != "unchanged" || frame == 0)
            {
                if (mode is "cold-full" or "dirty-world" or "replacement")
                {
                    reference.SetScene(null);
                    fresh = reference.SetScene(new(observation));
                }
                else reference.InvalidateFrame();
                reference.Draw(expected.Canvas, fresh, camera, width, height, size.Density, null, visual: visual);
                if (!ImagesEqual(surface, expected))
                {
                    NativeMapMatrix.Encode(surface, Path.ChangeExtension(output, "actual.png"));
                    NativeMapMatrix.Encode(expected, Path.ChangeExtension(output, "expected.png"));
                    throw new InvalidOperationException($"{size.Width} {mode} run {run} frame {frame} with {stats.Redraw} differs from a fresh image");
                }
                verified++;
            }
        }
        int distinctHalf = origin.Visible(observation, width, height).Select(item => item.Point).Distinct().Count();
        Console.WriteLine($"{size.Width}×{size.Height} {size.Density}× {mode} run {run}: p95 {Quantile(samples.Select(s => s.FrameMs), .95):F2} ms; {verified} equal frames");
        return new
        {
            size.Width,
            size.Height,
            size.Density,
            run,
            mode,
            halfZoom = half,
            minZoom,
            maxZoom,
            halfDistinctTiles = distinctHalf,
            totalTiles = observation.Tiles.Count,
            observedUnits = observation.Units.Count,
            observedCities = observation.Cities.Count,
            verified,
            p50Ms = Quantile(samples.Select(s => s.FrameMs), .50),
            p95Ms = Quantile(samples.Select(s => s.FrameMs), .95),
            p99Ms = Quantile(samples.Select(s => s.FrameMs), .99),
            maxMs = samples.Max(s => s.FrameMs),
            samples
        };
    }

    private static object Replacements(AssetAtlas atlas, PinnedFonts fonts, ScopedMapPerformanceWorld fixture)
    {
        using var painter = new MapPainter(atlas, fonts);
        using var surface = SKSurface.Create(new SKImageInfo(960, 600));
        var records = new List<object>();
        var camera = new MapCamera(80, 60, 1);
        for (int frame = 0; frame < 110; frame++)
        {
            fixture.Switch(frame);
            var observation = fixture.Capture();
            long epoch = painter.SetScene(new(observation));
            NativeMapMatrix.Require(painter.GroundUsage == (0, 0), $"observer replacement {frame} releases the previous observer's ground");
            NativeMapMatrix.Require(painter.ViewportBytes == 0, $"observer replacement {frame} releases the previous viewport surfaces");
            painter.Draw(surface.Canvas, epoch, camera, 960, 600, 1, null);
            var panned = painter.Draw(surface.Canvas, epoch, camera.Pan(1, 0, observation, 960, 600), 960, 600, 1, null);
            NativeMapMatrix.Require(panned.Redraw == MapRedraw.Scrolled && painter.ViewportBytes == 3L * 960 * 600 * 4,
                $"observer replacement {frame} rebuilds the full retained scroll buffers");
            var (chunks, groundBytes) = painter.GroundUsage;
            var (count, bytes) = painter.TextureUsage;
            NativeMapMatrix.Require(groundBytes <= 240L * 1024 * 1024 && bytes <= 96L * 1024 * 1024,
                $"observer replacement {frame} remains within declared raster budgets");
            if (frame % 10 == 9)
            {
                records.Add(new { frame, count, bytes, chunks, groundBytes, viewportBytes = painter.ViewportBytes, retainedManagedBytes = GC.GetTotalMemory(true) });
            }
        }
        return records;
    }

    private static bool ImagesEqual(SKSurface a, SKSurface b)
    {
        using var first = a.PeekPixels();
        using var second = b.PeekPixels();
        return first.GetPixelSpan().SequenceEqual(second.GetPixelSpan());
    }

    private static double Quantile(IEnumerable<double> values, double p)
    {
        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Ceiling(sorted.Length * p) - 1];
    }

}
