using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

/// <summary>Authored navigation histories and disclosed-data changes. A failed
/// experimental pixel gate is recorded, never converted into a looser tolerance.</summary>
internal static class GpuOptimizationChecks
{
    public static void Run(GRContext context, AssetAtlas atlas, PinnedFonts fonts, string output, string fixtures,
        string renderer, string version, bool experimentalMaskDiagnostics = false)
    {
        Directory.CreateDirectory(output);
        var (native, classic) = NativeMapMatrix.OverrideChecks(fixtures);
        var rows = new List<object>();
        // Stable-layout masks are a separate experiment, not part of the supported profile.
        // Browser callers retain its strict diagnostic results without admitting that experiment.
        var masks = GpuMaskChecks.Run(context, MapGpuComposition.Features.HasFlag(MapGpuFeatures.MaskBatch),
            enforce: !experimentalMaskDiagnostics);
        var damage = DamageChecks(atlas, fonts);
        int failures = 0, checks = 0;
        foreach (var (theme, art) in new (string, MapArtSelection?)[] { ("stock", null), ("native", native), ("classic", classic) })
            foreach (int density in new[] { 1, 2 })
            {
                using var painter = new MapPainter(atlas, fonts, art);
                var board = MapGallery.Open("Mixed");
                var scene = new MapScene(board.Map);
                var camera = NativeMapMatrix.BoardCamera(board.Map);
                foreach (var (id, dx, dy) in new (string, double, double)[]
                {
                    ("initial", 0, 0), ("fractional", .25, .5), ("integer", 1, 1), ("diagonal", 13.5, -7.25),
                    ("region-boundary", 770, 769), ("reverse", .25, .5), ("return", 0, 0)
                }) Check(id, scene, camera with { X = camera.X + dx, Y = camera.Y + dy });
                Check("seam", scene, camera with { X = camera.X - scene.Map.Width * 32 * camera.Zoom + .25 });
                foreach (double zoom in new[] { .5499, .5501, .6249, .6251, 1.2499, 1.2501, 2.4999, 2.5001, 1.2501, .6251, .5501 })
                    Check("zoom-" + zoom.ToString(System.Globalization.CultureInfo.InvariantCulture), scene, camera with { Zoom = zoom });
                var a = camera.Hit(160, 270, scene.Map) ?? new(4, 2);
                var b = camera.Hit(800, 270, scene.Map) ?? new(20, 2);
                var tiles = scene.Map.Tiles.ToArray();
                foreach (var point in new[] { a, b })
                {
                    int index = point.Y * (scene.Map.Width / 2) + point.X / 2;
                    tiles[index] = tiles[index] with { Improvements = tiles[index].Improvements ^ MapFeatures.Road };
                }
                scene = new(MapObservationDelta.Attach(scene.Map, scene.Map with { Revision = scene.Map.Revision + 1, Tiles = new(tiles) }));
                Check("separated-edits", scene, camera);
                // A copied delta cannot certify another tile list, skipped revision or observer.
                scene = new(scene.Map with { Revision = scene.Map.Revision + 2, Tiles = new(scene.Map.Tiles) });
                Check("skipped-stale-delta", scene, camera);
                for (int color = 0; color < 8; color++)
                {
                    scene = new(MapObservationDelta.Attach(scene.Map, MapGallery.ChangeOwnerColors(scene.Map)));
                    Check("recolor-" + color, scene, camera);
                }
                Check("moving-effects", scene, camera, NativeMapMatrix.FeatureFrame(scene));
                painter.RecoverResources();
                Check("recovery", scene, camera);
                painter.SelectArt(theme == "stock" ? native : null);
                // The local art selection below follows the replacement for fresh/current comparisons.
                var replacement = theme == "stock" ? native : null;
                Check("art-replacement", scene, camera, replacementArt: replacement, replaced: true);
                painter.SelectArt(art);
                scene = new(scene.Map with { Identity = scene.Map.Identity with { Observer = new(2) }, Units = new([]), Cities = new([]) });
                Check("observer-replacement", scene, camera);
                var flat = new MapScene(board.Map with { Flat = true });
                Check("flat-edge", flat, camera with { X = 64.25, Y = 32.5 });

                void Check(string id, MapScene value, MapCamera view, VisualFrame? visual = null,
                    MapArtSelection? replacementArt = null, bool replaced = false)
                {
                    const int width = 960, height = 540;
                    var info = new SKImageInfo(width * density, height * density, SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
                    using var actual = SKSurface.Create(context, true, info);
                    using var fresh = SKSurface.Create(context, true, info);
                    using var current = SKSurface.Create(context, true, info);
                    using var cpu = SKSurface.Create(info);
                    using var cpuPainter = new MapPainter(atlas, fonts, replaced ? replacementArt : art, parallelForeground: false, packGroundTextures: false);
                    using var freshPainter = new MapPainter(atlas, fonts, replaced ? replacementArt : art);
                    using var currentPainter = new MapPainter(atlas, fonts, replaced ? replacementArt : art);
                    actual.Canvas.Scale(density); fresh.Canvas.Scale(density); current.Canvas.Scale(density); cpu.Canvas.Scale(density);
                    var stats = MapGpuComposition.Draw(painter, context, actual.Canvas, painter.SetScene(value), view, width, height, density, null, board.Display, visual);
                    var freshStats = MapGpuComposition.Draw(freshPainter, context, fresh.Canvas, freshPainter.SetScene(value), view, width, height, density, null, board.Display, visual);
                    var baseline = MapGpuComposition.Draw(currentPainter, context, current.Canvas, currentPainter.SetScene(value), view, width, height, density, null, board.Display, visual,
                        features: MapGpuComposition.SupportedFeatures);
                    var cpuStats = cpuPainter.Draw(cpu.Canvas, cpuPainter.SetScene(value), view, width, height, density, null, board.Display, visual);
                    NativeMapMatrix.Require(cpuStats.Error is null && stats.Tiles == cpuStats.Tiles && stats.Objects == cpuStats.Objects &&
                        stats.Labels == cpuStats.Labels && stats.Effects == cpuStats.Effects, "CPU/GPU semantic counts " + id);
                    NativeMapMatrix.Require(stats.Error is null && freshStats.Error is null && baseline.Error is null && stats.Tiles == baseline.Tiles &&
                        stats.Objects == baseline.Objects && stats.Labels == baseline.Labels && stats.Effects == baseline.Effects, "optimization semantic counts " + id);
                    using var read = GpuPixelChecks.Read(actual);
                    using var freshRead = GpuPixelChecks.Read(fresh);
                    using var baselineRead = GpuPixelChecks.Read(current);
                    var history = GpuPixelChecks.Compare(read.GetPixelSpan(), freshRead.GetPixelSpan());
                    var difference = GpuPixelChecks.Compare(read.GetPixelSpan(), baselineRead.GetPixelSpan());
                    using var cpuPixels = cpu.PeekPixels();
                    var cpuDifference = GpuPixelChecks.Compare(read.GetPixelSpan(), cpuPixels.GetPixelSpan());
                    bool cpuWithinBounds = cpuDifference.MeanChannelError <= 2 && cpuDifference.PixelsOver8 <= cpuDifference.Pixels * .10;
                    if (history.DifferentPixels != 0) failures++;
                    checks++;
                    rows.Add(new { id, theme, density, history, currentGpu = difference, cpu = cpuDifference, cpuWithinBounds, stats });
                    Console.WriteLine($"GPU history: {theme}/{density} {id}, exact={history.DifferentPixels == 0}, CPU bounds={cpuWithinBounds}");
                    if (theme == "stock" && density == 1 && (history.DifferentPixels != 0 || id is "initial" or "fractional" or "separated-edits"))
                    {
                        NativeMapMatrix.Encode(actual, Path.Combine(output, id + "-candidate.png"));
                        NativeMapMatrix.Encode(current, Path.Combine(output, id + "-current.png"));
                    }
                }
            }
        object lifecycle;
        Exception? replacementFailure = null;
        try { lifecycle = ReplacementChecks(context, atlas, fonts, output); }
        catch (Exception error)
        {
            replacementFailure = error;
            lifecycle = new { error = error.Message };
        }
        File.WriteAllText(Path.Combine(output, "optimization-checks.json"), JsonSerializer.Serialize(new
        {
            format = "openciv2-gpu-optimization-checks/1",
            Renderer = renderer,
            Version = version,
            identity = JsonDocument.Parse(ApplicationIdentity.Read(typeof(GpuOptimizationChecks).Assembly)).RootElement,
            features = MapGpuComposition.Features.ToString(),
            checks,
            historyFailures = failures,
            lifecycle,
            masks,
            damage,
            rows
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Optimization checks: {checks}; exact history failures: {failures}");
        if (replacementFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(replacementFailure).Throw();
    }

    private static object ReplacementChecks(GRContext context, AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        var board = MapGallery.Open("Mixed");
        var camera = NativeMapMatrix.BoardCamera(board.Map);
        using var painter = new MapPainter(atlas, fonts);
        var sizes = new Dictionary<int, (long Pages, long Sources, long Textures, long Ground, long Viewport)>();
        var rows = new List<object>();
        for (int cycle = 0; cycle < 110; cycle++)
        {
            int phase = cycle % 2, density = 1 + phase, width = phase == 0 ? 800 : 960, height = 540;
            var map = board.Map with { Identity = board.Map.Identity with { Observer = new((byte)(1 + phase)) } };
            long epoch = painter.SetScene(new(map));
            if (cycle % 10 == 0) painter.RecoverResources();
            NativeMapMatrix.Require(painter.AtlasUsage == (0L, 0L) && painter.OptimizationUsage == (0L, 0L),
                "observer replacement releases outgoing GPU caches");
            var info = new SKImageInfo(width * density, height * density, SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
            using var actual = SKSurface.Create(context, true, info);
            using var fresh = SKSurface.Create(context, true, info);
            using var reference = new MapPainter(atlas, fonts);
            actual.Canvas.Scale(density); fresh.Canvas.Scale(density);
            var stats = MapGpuComposition.Draw(painter, context, actual.Canvas, epoch, camera, width, height, density, null);
            var freshStats = MapGpuComposition.Draw(reference, context, fresh.Canvas, reference.SetScene(new(map)), camera, width, height, density, null);
            using var actualPixels = GpuPixelChecks.Read(actual);
            using var freshPixels = GpuPixelChecks.Read(fresh);
            if (stats.Error is not null || freshStats.Error is not null || !actualPixels.GetPixelSpan().SequenceEqual(freshPixels.GetPixelSpan()))
            {
                NativeMapMatrix.Encode(actual, Path.Combine(output, $"replacement-{cycle}-actual.png"));
                NativeMapMatrix.Encode(fresh, Path.Combine(output, $"replacement-{cycle}-fresh.png"));
                var difference = GpuPixelChecks.Compare(actualPixels.GetPixelSpan(), freshPixels.GetPixelSpan());
                File.WriteAllText(Path.Combine(output, "replacement-failure.json"), JsonSerializer.Serialize(new
                {
                    cycle,
                    density,
                    width,
                    height,
                    stats,
                    freshStats,
                    difference,
                    byteExact = false
                }));
                throw new InvalidOperationException($"GPU replacement {cycle} differs from fresh pixels: {difference}; actual error={stats.Error}, fresh error={freshStats.Error}");
            }
            NativeMapMatrix.Require(painter.AtlasUsage.Pages <= 64L * 1024 * 1024 && painter.AtlasUsage.Sources <= 64L * 1024 * 1024 &&
                stats.GroundCacheBytes <= stats.GroundCacheLimitBytes, "GPU replacement caches stay bounded");
            if (cycle < 10) continue;
            var size = (painter.AtlasUsage.Pages, painter.AtlasUsage.Sources, painter.TextureUsage.PixelBytes,
                painter.GroundUsage.PixelBytes, painter.ViewportBytes);
            if (sizes.TryGetValue(phase, out var prior)) NativeMapMatrix.Require(size == prior, "warmed GPU replacement caches do not grow");
            else sizes.Add(phase, size);
            rows.Add(new { cycle, density, width, height, stats.GroundCacheBytes, stats.GroundCacheLimitBytes, painter.AtlasUsage });
        }
        return new { warmup = 10, warmedReplacements = rows.Count, exact = true, bounded = true, rows };
    }

    private static object DamageChecks(AssetAtlas atlas, PinnedFonts fonts)
    {
        using var painter = new MapPainter(atlas, fonts);
        var before = new MapScene(MapGallery.Open("Mixed").Map);
        var tiles = before.Map.Tiles.ToArray();
        foreach (var point in new MapPoint[] { new(6, 8), new(38, 8) })
        {
            int i = point.Y * (before.Map.Width / 2) + point.X / 2;
            tiles[i] = tiles[i] with { Terrain = 2, Improvements = tiles[i].Improvements ^ MapFeatures.Road };
        }
        var after = new MapScene(MapObservationDelta.Attach(before.Map, before.Map with { Revision = before.Map.Revision + 1, Tiles = new(tiles) }));
        var camera = new MapCamera(96, 72, 1);
        var sparse = painter.InspectDamage(before, after, camera, 1920, 1080, 1, true);
        var full = painter.InspectDamage(before, after, camera, 1920, 1080, 1, false);
        NativeMapMatrix.Require(sparse.ComparedTiles == 2 && sparse.Regions is { Count: 2 } && full.Regions is { Count: 2 } &&
            sparse.Regions.SequenceEqual(full.Regions), "two distant disclosed edits remain two regions with only two comparisons");
        var stale = new MapScene(after.Map with { Tiles = new(after.Map.Tiles) });
        var fallback = painter.InspectDamage(before, stale, camera, 1920, 1080, 1, true);
        NativeMapMatrix.Require(fallback.ComparedTiles == before.Map.Tiles.Count && fallback.Regions!.SequenceEqual(full.Regions!),
            "mismatched snapshot metadata falls back to full comparison");
        return new
        {
            sparseCompared = sparse.ComparedTiles,
            fullCompared = full.ComparedTiles,
            fallbackCompared = fallback.ComparedTiles,
            regions = sparse.Regions!.Select(box => new { box.Left, box.Top, box.Right, box.Bottom }).ToArray()
        };
    }
}
