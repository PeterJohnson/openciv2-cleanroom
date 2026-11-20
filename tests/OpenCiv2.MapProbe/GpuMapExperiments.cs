using System.Diagnostics;
using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

/// <summary>Replays existing native input states with GL instrumentation and bounded frames in flight.</summary>
internal static class GpuMapExperiments
{
    private sealed record Variant(string Name, bool Gpu, MapGpuFeatures Features);
    private static readonly Variant[] Variants =
    [
        new("cpu", false, MapGpuFeatures.None), new("gpu", true, MapGpuFeatures.None),
        new("atlas", true, MapGpuFeatures.Atlas), new("offload", true, MapGpuFeatures.Offload),
        new("retain", true, MapGpuFeatures.Retain), new("direct", true, MapGpuFeatures.Direct),
        new("combined", true, MapGpuFeatures.Atlas | MapGpuFeatures.Offload | MapGpuFeatures.Retain | MapGpuFeatures.Direct),
        new("atlas-direct", true, MapGpuFeatures.Atlas | MapGpuFeatures.Direct),
        new("atlas-textures", true, MapGpuFeatures.Atlas | MapGpuFeatures.Textures | MapGpuFeatures.Direct),
        new("atlas-terrain", true, MapGpuFeatures.Atlas | MapGpuFeatures.Terrain | MapGpuFeatures.Direct),
        new("streamed", true, MapGpuFeatures.Atlas | MapGpuFeatures.Textures | MapGpuFeatures.Direct | MapGpuFeatures.Encoded),
        new("commands", true, MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands),
        new("sparse", true, MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands | MapGpuFeatures.Sparse),
        new("masks", true, MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands | MapGpuFeatures.Masks),
        new("mask-batch", true, MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands | MapGpuFeatures.Masks | MapGpuFeatures.MaskBatch),
        new("stable", true, MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands | MapGpuFeatures.StableAtlas),
        new("stable-masks", true, MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands | MapGpuFeatures.Masks | MapGpuFeatures.StableAtlas),
        new("pan-cache", true, MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands | MapGpuFeatures.PanCache | MapGpuFeatures.Sparse),
        new("pan-phase", true, MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands | MapGpuFeatures.PanCache | MapGpuFeatures.PanPhase | MapGpuFeatures.Sparse),
        new("tier-cache", true, MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands | MapGpuFeatures.PanCache | MapGpuFeatures.TierCache | MapGpuFeatures.Sparse),
        new("ground-plans", true, MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands | MapGpuFeatures.Terrain | MapGpuFeatures.GroundPlans | MapGpuFeatures.Sparse),
        new("optimized", true, MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands | MapGpuFeatures.Masks | MapGpuFeatures.StableAtlas | MapGpuFeatures.Sparse),
        new("candidate", true, MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands | MapGpuFeatures.Masks | MapGpuFeatures.MaskBatch | MapGpuFeatures.Sparse),
        new("textures-direct", true, MapGpuFeatures.Textures | MapGpuFeatures.Direct | MapGpuFeatures.Encoded),
        new("full-streamed", true, MapGpuFeatures.Atlas | MapGpuFeatures.Offload | MapGpuFeatures.Direct | MapGpuFeatures.Encoded)
    ];

    public static void Run(AssetAtlas atlas, PinnedFonts fonts, string input, string output)
    {
        using var receipt = JsonDocument.Parse(File.ReadAllBytes(input));
        using var gl = new MapProbeGlContext(profile: true);
        var board = MapGallery.Open("Mixed");
        var results = new List<object>();
        int count = int.TryParse(Environment.GetEnvironmentVariable("OPENCIV2_PROFILE_FRAMES"), out int n) ? n : 120;
        int[] depths = (Environment.GetEnvironmentVariable("OPENCIV2_PROFILE_DEPTHS") ?? "1").Split(',').Select(int.Parse).ToArray();
        var names = (Environment.GetEnvironmentVariable("OPENCIV2_PROFILE_VARIANTS") ?? "cpu,gpu,atlas,offload,retain,direct,combined,atlas-direct,atlas-textures,atlas-terrain").Split(',');
        var variants = Variants.Where(v => names.Contains(v.Name)).ToArray();
        if (variants.Length == 0 || depths.Any(d => d is < 1 or > 3) || count < 1) throw new ArgumentException("Invalid GPU experiment configuration");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        foreach (var series in receipt.RootElement.GetProperty("results").EnumerateArray())
        {
            string mode = series.GetProperty("mode").GetString()!;
            if (mode is not ("pan" or "continuous-zoom" or "dirty-world" or "local-dirty")) continue;
            int run = series.GetProperty("run").GetInt32();
            // Rotate order across independent source runs to reduce warm/thermal bias.
            foreach (var variant in variants.Skip(run % variants.Length).Concat(variants.Take(run % variants.Length)))
                foreach (int depth in depths)
                {
                    gl.Context.Flush(submit: true, synchronous: true);
                    gl.Context.PurgeUnlockedResources(false);
                    MapGpuComposition.PrepareContext(gl.Context);
                    int width = series.GetProperty("width").GetInt32(), height = series.GetProperty("height").GetInt32();
                    double density = series.GetProperty("density").GetDouble();
                    var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
                    var targets = Enumerable.Range(0, depth).Select(_ => SKSurface.Create(gl.Context, true, info)).ToArray();
                    using var painter = new MapPainter(atlas, fonts);
                    var world = board.Map with { Revision = series.GetProperty("initialRevision").GetUInt64() };
                    bool dirty = mode is "dirty-world" or "local-dirty";
                    bool hasWarmup = series.TryGetProperty("warmupSamples", out var warmupSamples);
                    var samples = (hasWarmup ? warmupSamples.EnumerateArray().Concat(series.GetProperty("samples").EnumerateArray()) :
                        series.GetProperty("samples").EnumerateArray()).ToArray();
                    MapPoint? change = dirty ? new(samples[0].GetProperty("updateX").GetInt32(), samples[0].GetProperty("updateY").GetInt32()) : null;
                    if (dirty && !hasWarmup)
                        for (int i = 0; i < series.GetProperty("warmup").GetInt32(); i++) world = Change(world);
                    long epoch = painter.SetScene(new(world));
                    const int warm = 30;
                    var measurements = new List<Dictionary<string, object>>();
                    var clock = MapGlProfiler.Calibrate();
                    var pending = new Queue<(MapGlProfiler.Frame Fence, Dictionary<string, object> Row, bool Measured)>();
                    long measuredStart = 0, engineStart = 0;
                    int limit = Math.Min(samples.Length, warm + count);
                    try
                    {
                        for (int i = 0; i < limit; i++)
                        {
                            if (i == warm)
                            {
                                while (pending.Count > 0) Complete();
                                engineStart = GraphicsNanoseconds();
                                measuredStart = Stopwatch.GetTimestamp();
                            }
                            if (pending.Count >= depth) Complete();
                            var row = samples[i];
                            long inputStart = Stopwatch.GetTimestamp();
                            if (dirty) { world = Change(world); epoch = painter.SetScene(new(world)); }
                            var camera = new MapCamera(row.GetProperty("x").GetDouble(), row.GetProperty("y").GetDouble(), row.GetProperty("zoom").GetDouble());
                            var visual = row.TryGetProperty("presentation", out var presentation) && presentation.ValueKind == JsonValueKind.Object
                                ? NativeMapNavigationReplay.Presentation(row, change, row.GetProperty("effects").GetInt32()) : null;
                            var target = targets[i % depth];
                            target.Canvas.Save();
                            target.Canvas.Scale((float)density);
                            var before = gl.Profiler!.Counts;
                            var fence = new MapGlProfiler.Frame(clock);
                            fence.Begin();
                            var stats = variant.Gpu
                                ? MapGpuComposition.Draw(painter, gl.Context, target.Canvas, epoch, camera, width / density, height / density,
                                    density, null, board.Display, visual, features: variant.Features)
                                : painter.Draw(target.Canvas, epoch, camera, width / density, height / density, density, null, board.Display, visual);
                            target.Canvas.Restore();
                            gl.Context.Flush(submit: true, synchronous: false);
                            fence.Submit();
                            double submitMs = Stopwatch.GetElapsedTime(inputStart).TotalMilliseconds;
                            NativeMapMatrix.Require(stats.Error is null && stats.Tiles == row.GetProperty("tiles").GetInt32() &&
                                stats.Objects == row.GetProperty("objects").GetInt32() && stats.Labels == row.GetProperty("labels").GetInt32() &&
                                stats.Effects == row.GetProperty("effects").GetInt32(),
                                $"profile observations {variant.Name}/{mode}/{i}");
                            var counters = MapGlProfiler.Delta(before, gl.Profiler.Counts);
                            gl.Context.GetResourceCacheUsage(out int resources, out long bytes);
                            var sample = new Dictionary<string, object>
                            {
                                ["frame"] = i,
                                ["submitMs"] = submitMs,
                                ["updateMs"] = Stopwatch.GetElapsedTime(inputStart, fence.StartedAt).TotalMilliseconds,
                                ["drawCalls"] = counters.Draws,
                                ["uploadCalls"] = counters.UploadCalls,
                                ["uploadBytes"] = counters.UploadBytes,
                                ["textureBinds"] = counters.TextureBinds,
                                ["textureCreates"] = counters.TextureCreates,
                                ["textureDeletes"] = counters.TextureDeletes,
                                ["gpuCacheBytes"] = bytes,
                                ["gpuResources"] = resources,
                                ["terrainMs"] = stats.TerrainMs,
                                ["foregroundMs"] = stats.ForegroundMs,
                                ["foregroundPlanMs"] = stats.ForegroundPlanMs,
                                ["atlasPrepareMs"] = stats.AtlasPrepareMs,
                                ["foregroundSubmitMs"] = stats.ForegroundSubmitMs,
                                ["foregroundTraversals"] = stats.ForegroundTraversals,
                                ["foregroundCommands"] = stats.ForegroundCommands,
                                ["drawAllocatedBytes"] = stats.DrawAllocatedBytes,
                                ["texturePrepareMs"] = stats.TexturePrepareMs,
                                ["demandRasterLoads"] = stats.DemandRasterLoads,
                                ["preparedChunks"] = stats.PreparedChunks,
                                ["groundCacheBytes"] = stats.GroundCacheBytes,
                                ["textureCacheBytes"] = stats.TextureCacheBytes,
                                ["atlasBytes"] = stats.AtlasBytes,
                                ["atlasAdmissions"] = stats.AtlasAdmissions,
                                ["atlasEvictions"] = stats.AtlasEvictions,
                                ["atlasHits"] = stats.AtlasHits,
                                ["regionBuilds"] = stats.RegionBuilds,
                                ["regionHits"] = stats.RegionHits,
                                ["regionBytes"] = stats.RegionBytes,
                                ["regionEvictions"] = stats.RegionEvictions,
                                ["groundComparedTiles"] = stats.GroundComparedTiles,
                                ["damageComparedTiles"] = stats.DamageComparedTiles,
                                ["damageRegions"] = stats.DamageRegions,
                                ["topologyHits"] = stats.TopologyHits,
                                ["topologyBuilds"] = stats.TopologyBuilds,
                                ["topologyEvictions"] = stats.TopologyEvictions,
                                ["topologyBytes"] = stats.TopologyBytes,
                                ["encodedSourceBytes"] = stats.EncodedSourceBytes,
                                ["spriteCalls"] = stats.SpriteCalls,
                                ["atlasBatches"] = stats.AtlasBatches,
                                ["retainedPatches"] = stats.RetainedPatches
                            };
                            pending.Enqueue((fence, sample, i >= warm));
                            // Observe completed old frames without introducing a wait.
                            while (pending.TryPeek(out var ready) && ready.Fence.Ready) Complete();
                        }
                        while (pending.Count > 0) Complete();
                        double elapsed = Stopwatch.GetElapsedTime(measuredStart).TotalMilliseconds;
                        long engineNs = GraphicsNanoseconds() - engineStart;
                        results.Add(new
                        {
                            width,
                            height,
                            density,
                            mode,
                            run,
                            variant = variant.Name,
                            features = variant.Features.ToString(),
                            depth,
                            warmup = warm,
                            frames = measurements.Count,
                            elapsedMs = elapsed,
                            throughputFps = measurements.Count * 1000 / elapsed,
                            graphicsEngineNs = engineNs,
                            graphicsEngineMsPerFrame = engineNs / (measurements.Count * 1e6),
                            clockUncertaintyMs = clock.UncertaintyMs,
                            cacheLimitBytes = MapGpuComposition.CacheLimitBytes,
                            completedMs = Quantiles(measurements, "completedMs"),
                            gpuCompletedMs = Quantiles(measurements, "gpuCompletedMs"),
                            submitMs = Quantiles(measurements, "submitMs"),
                            gpuStreamMs = Quantiles(measurements, "gpuStreamMs"),
                            samples = measurements
                        });
                        Console.WriteLine($"{width}x{height} {mode} run {run} {variant.Name} depth {depth}: {measurements.Count * 1000 / elapsed:F1} fps");
                        Save();
                    }
                    finally
                    {
                        while (pending.Count > 0) { using var abandoned = pending.Dequeue().Fence; abandoned.Complete(); }
                        foreach (var target in targets) target.Dispose();
                    }

                    void Complete()
                    {
                        var item = pending.Dequeue();
                        using (item.Fence)
                        {
                            var (stream, observed, gpuCompletion) = item.Fence.Complete();
                            item.Row["gpuStreamMs"] = stream;
                            item.Row["completedMs"] = observed + (double)item.Row["updateMs"];
                            item.Row["gpuCompletedMs"] = gpuCompletion + (double)item.Row["updateMs"];
                            if (item.Measured) measurements.Add(item.Row);
                        }
                    }

                    MapObservation Change(MapObservation map)
                    {
                        if (mode == "local-dirty") return LocalChange(map, change!.Value);
                        var after = MapGallery.Changed(map, change);
                        return variant.Features.HasFlag(MapGpuFeatures.Sparse) ? MapObservationDelta.Attach(map, after) : after;
                    }
                }
        }

        void Save() => File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            format = "openciv2-gpu-experiments/1",
            recordedNavigationPulse = true,
            gl.Renderer,
            gl.Version,
            scope = "Existing native input states replayed offscreen. GL timestamp interval includes CPU feed stalls, not isolated GPU busy time. GPU completion uses host-correlated timestamps; the separate host-observed fence completion is an upper bound. Throughput includes draining all measured work. No compositor or physical presentation.",
            input,
            inputSha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(input))),
            identity = JsonDocument.Parse(ApplicationIdentity.Read(typeof(GpuMapExperiments).Assembly)).RootElement,
            results
        }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }

    internal static MapObservation LocalChange(MapObservation map, MapPoint point)
    {
        var tiles = map.Tiles.ToArray();
        int index = point.Y * (map.Width / 2) + point.X / 2;
        tiles[index] = tiles[index] with { Improvements = tiles[index].Improvements ^ MapImprovements.Road };
        return MapObservationDelta.Attach(map, map with { Revision = map.Revision + 1, Tiles = new(tiles) });
    }

    private static Dictionary<string, double> Quantiles(List<Dictionary<string, object>> rows, string key)
    {
        var sorted = rows.Select(r => Convert.ToDouble(r[key])).Order().ToArray();
        return new[] { 50, 95, 99 }.ToDictionary(p => "p" + p, p => sorted[(int)Math.Ceiling(sorted.Length * p / 100d) - 1]);
    }

    private static long GraphicsNanoseconds()
    {
        var clients = new Dictionary<string, long>();
        foreach (string file in Directory.EnumerateFiles("/proc/self/fdinfo"))
        {
            string[] lines;
            try { lines = File.ReadAllLines(file); } catch (IOException) { continue; }
            string? client = lines.FirstOrDefault(line => line.StartsWith("drm-client-id:", StringComparison.Ordinal));
            string? counter = lines.FirstOrDefault(line => line.StartsWith("drm-engine-gfx:", StringComparison.Ordinal) ||
                line.StartsWith("drm-engine-render:", StringComparison.Ordinal));
            if (client is null || counter is null) continue;
            long value = long.Parse(counter.Split(':')[1].Trim().Split(' ')[0]);
            clients[client] = Math.Max(value, clients.GetValueOrDefault(client));
        }
        return clients.Values.Sum();
    }
}
