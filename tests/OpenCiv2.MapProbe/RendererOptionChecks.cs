using System.Text;
using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Formats.Graphics;
using SkiaSharp;

/// <summary>MV29/MV30: startup selection, controlled fallback, recovery and observer resource ownership.</summary>
internal static class RendererOptionChecks
{
    public static void Run(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        int checks = 0;
        void Require(bool value, string name) { NativeMapMatrix.Require(value, name); checks++; }
        foreach (var mode in Enum.GetValues<MapRendererMode>())
            foreach (int budget in new[] { 128, 512, 1024 })
            {
                var original = new MapRendererOptions(mode, budget);
                using var bytes = new MemoryStream();
                original.Write(bytes);
                bytes.Position = 0;
                Require(MapRendererOptions.Read(bytes) == original, "preference round trip");
            }
        foreach (string invalid in new[] { "{}", "[]", "{\"format\":1}",
            "{\"format\":\"openciv2-map-renderer/1\",\"renderer\":\"auto\",\"gpuCacheMiB\":512}",
            "{\"format\":\"openciv2-map-renderer/1\",\"renderer\":\"gpu\",\"gpuCacheMiB\":0}" })
        {
            bool rejected = false;
            try { using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(invalid)); MapRendererOptions.Read(bytes); }
            catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException) { rejected = true; }
            Require(rejected, "invalid preferences rejected");
        }
        var preferences = new MapRendererPreferences(new(), new(), _ => throw new IOException("test write failure"));
        try { preferences.Save(MapRendererMode.Gpu); }
        catch (IOException) { }
        Require(preferences.Saved.Mode == MapRendererMode.Cpu, "failed save preserves saved choice");
        var boundedAtlas = new MapGpuSpriteAtlas();
        using (var bitmap = new SKBitmap(64, 64))
        using (var sprite = SKImage.FromBitmap(bitmap))
        {
            for (int i = 0; i < 40; i++)
            {
                boundedAtlas.BeginPreparation();
                boundedAtlas.Collect(new("fixture-" + i, 64, 64, null), sprite);
                boundedAtlas.FinishPreparation();
                Require(boundedAtlas.LayoutCount <= 16 && boundedAtlas.Bytes <= 64L * 1024 * 1024, "layout cache stays bounded");
            }
            boundedAtlas.Clear();
            Require(boundedAtlas.Bytes == 0 && boundedAtlas.LayoutCount == 0, "layout cache releases all pages");
        }
        var options = new MapRendererOptions(MapRendererMode.Gpu);
        var session = new MapRendererSession(options);
        int resets = 0;
        MapDrawStats Draw(bool gpu) => new(MapRedraw.Full, 1, 1, 1);
        var fallback = session.Draw(null, Draw, () => resets++);
        Require(fallback.Composition == "cpu" && fallback.RequestedRenderer == "gpu" && fallback.RendererFallback is not null && resets == 1,
            "missing context falls back once");
        session.Draw(null, Draw, () => resets++);
        Require(resets == 1, "fallback stays stable");
        bool strict = false;
        try { new MapRendererSession(options).Draw(null, Draw, () => { }, strict: true); }
        catch (MapGpuResourceException) { strict = true; }
        Require(strict, "benchmark cannot silently fall back");

        using var painter = new MapPainter(atlas, fonts, options: options);
        var board = MapGallery.Open("Mixed");
        var camera = NativeMapMatrix.BoardCamera(board.Map);
        using var cpu = SKSurface.Create(new SKImageInfo(960, 540));
        long epoch = painter.SetScene(new(board.Map));
        var cpuFallback = session.Draw(null, gpu => painter.Draw(cpu.Canvas, epoch, camera, 960, 540, 1, null), painter.RecoverResources);
        Require(cpuFallback.Error is null && cpuFallback.Tiles > 0 && cpuFallback.Composition == "cpu", "fallback draws the complete map");
        var samples = new List<object>();
        var retainedSizes = new Dictionary<(int Context, int Phase), (long Pages, long Sources, long Textures, long Ground, long Viewport, long Regions, long Topology)>();
        // Repeat replacements after warm-up, including new observers, densities,
        // viewport sizes, texture eviction, context replacement and recovery.
        for (int contextIndex = 0; contextIndex < 2; contextIndex++)
        {
            using var gl = new MapProbeGlContext();
            session.Retry();
            if (contextIndex == 0)
            {
                var allocation = new MapRendererSession(options).Draw(gl.Context,
                    gpu => gpu ? throw new MapGpuResourceException("injected allocation failure") : Draw(false), () => resets++);
                Require(allocation.Composition == "cpu" && allocation.RendererFallback == "injected allocation failure", "GPU allocation fallback");
                bool contentPropagated = false;
                try { new MapRendererSession(options).Draw(gl.Context, _ => throw new InvalidDataException("invalid content"), () => { }); }
                catch (InvalidDataException) { contentPropagated = true; }
                Require(contentPropagated, "content error is not a capability failure");
                MapGpuComposition.PrepareContext(gl.Context, 256L * 1024 * 1024);
                MapGpuComposition.PrepareContext(gl.Context, 768L * 1024 * 1024);
                Require(gl.Context.GetResourceCacheLimit() == 256L * 1024 * 1024, "shared context keeps conservative ceiling");
            }
            for (int visit = 0; visit < 20; visit++)
            {
                var map = visit % 2 == 0 ? board.Map : MapGallery.Changed(board.Map);
                var view = camera with { Zoom = visit % 4 < 2 ? camera.Zoom : camera.Zoom * 1.25 };
                using var surface = SKSurface.Create(gl.Context, true, new SKImageInfo(960, 540));
                MapGpuComposition.Draw(painter, gl.Context, surface.Canvas, painter.SetScene(new(map)), view, 960, 540, 1, null);
                using var fresh = new MapPainter(atlas, fonts);
                using var reference = SKSurface.Create(gl.Context, true, new SKImageInfo(960, 540));
                MapGpuComposition.Draw(fresh, gl.Context, reference.Canvas, fresh.SetScene(new(map)), view, 960, 540, 1, null);
                using var actual = GpuMapMatrix.Read(surface);
                using var expected = GpuMapMatrix.Read(reference);
                Require(actual.GetPixelSpan().SequenceEqual(expected.GetPixelSpan()), "revisited atlas layout equals fresh layout");
            }
            painter.RecoverResources();
            for (int cycle = 0; cycle < 60; cycle++)
            {
                var map = board.Map with { Identity = board.Map.Identity with { Observer = new((byte)(1 + cycle % 2)) } };
                epoch = painter.SetScene(new(map));
                if (cycle % 10 == 0) painter.RecoverResources();
                Require(painter.AtlasUsage == (0L, 0L), "replacement releases outgoing atlas");
                Require(painter.OptimizationUsage == (0L, 0L), "replacement releases observer regions and topology");
                int density = 1 + cycle % 2, width = cycle % 2 == 0 ? 800 : 960, height = 540;
                var info = new SKImageInfo(width * density, height * density, SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
                using var surface = SKSurface.Create(gl.Context, true, info);
                surface.Canvas.Scale(density);
                var stats = session.Draw(gl.Context, gpu => gpu
                    ? MapGpuComposition.Draw(painter, gl.Context, surface.Canvas, epoch, camera, width, height, density, null)
                    : painter.Draw(surface.Canvas, epoch, camera, width, height, density, null), painter.RecoverResources);
                Require(stats.Error is null && stats.Composition == "gpu" && stats.RendererFallback is null, "retry/context replacement draws GPU map");
                Require(painter.AtlasUsage.Pages <= 64L * 1024 * 1024 && painter.AtlasUsage.Sources <= 64L * 1024 * 1024,
                    "atlas pages and admitted sources stay bounded");
                using var fresh = new MapPainter(atlas, fonts);
                using var reference = SKSurface.Create(gl.Context, true, info);
                reference.Canvas.Scale(density);
                MapGpuComposition.Draw(fresh, gl.Context, reference.Canvas, fresh.SetScene(new(map)), camera, width, height, density, null);
                using var actualPixels = GpuMapMatrix.Read(surface);
                using var expectedPixels = GpuMapMatrix.Read(reference);
                Require(actualPixels.GetPixelSpan().SequenceEqual(expectedPixels.GetPixelSpan()), "lifecycle frame equals fresh GPU frame");
                if (cycle >= 10)
                {
                    var size = (painter.AtlasUsage.Pages, painter.AtlasUsage.Sources, painter.TextureUsage.PixelBytes, painter.GroundUsage.PixelBytes, painter.ViewportBytes,
                        painter.OptimizationUsage.Regions, painter.OptimizationUsage.Topology);
                    var phase = (contextIndex, cycle % 2);
                    if (retainedSizes.TryGetValue(phase, out var prior)) Require(size == prior, "warmed retained cache sizes do not grow");
                    else retainedSizes.Add(phase, size);
                }
                if (cycle >= 10) samples.Add(new
                {
                    contextIndex,
                    cycle,
                    painter.AtlasUsage.Pages,
                    painter.AtlasUsage.Sources,
                    textures = painter.TextureUsage.PixelBytes,
                    ground = painter.GroundUsage.PixelBytes,
                    painter.ViewportBytes,
                    painter.OptimizationUsage.Regions,
                    painter.OptimizationUsage.Topology
                });
            }
            painter.RecoverResources();
            Require(painter.AtlasUsage == (0L, 0L), "release before host context disposal");
            gl.Context.AbandonContext();
            var lost = new MapRendererSession(options).Draw(gl.Context, Draw, () => { });
            Require(lost.Composition == "cpu" && lost.RendererFallback is not null, "abandoned context fallback");
        }
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "renderer-options.json"), JsonSerializer.Serialize(new { identity = JsonDocument.Parse(ApplicationIdentity.Read(typeof(RendererOptionChecks).Assembly)).RootElement, checks, warmedReplacements = samples.Count, samples }));
        Console.WriteLine($"Renderer options: {checks} checks, {samples.Count} warmed replacements passed");
    }
}
