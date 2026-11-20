using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static class GpuMapMatrix
{
    // Keep an independent pre-command baseline after Commands joins the supported set.
    private const MapGpuFeatures CommandBaseline = MapGpuFeatures.Atlas | MapGpuFeatures.Direct |
        MapGpuFeatures.Textures | MapGpuFeatures.Encoded;
    internal sealed record Difference(long Pixels, long DifferentPixels, long PixelsOver8, int MaxChannelError, double MeanChannelError);

    public static void Run(AssetAtlas atlas, PinnedFonts fonts, string output, string contentCases, bool experiment = false)
    {
        Directory.CreateDirectory(output);
        using var gl = new MapProbeGlContext();
        MapPresentationChecks.Run(atlas, fonts, output, gl.Context);
        var (native, classic) = NativeMapMatrix.OverrideChecks(contentCases);
        var results = new List<object>();
        foreach (var (theme, art) in new (string, MapArtSelection?)[] { ("stock", null), ("native", native), ("classic", classic) })
            foreach (int density in new[] { 1, 2 })
            {
                using var retained = new MapPainter(atlas, fonts, art);
                foreach (string id in MapGallery.Cases)
                {
                    var board = MapGallery.Open(id);
                    var scene = new MapScene(board.Map);
                    var camera = NativeMapMatrix.BoardCamera(board.Map);
                    VisualFrame? visual = id is "MV22" or "MV24" ? NativeMapMatrix.FeatureFrame(scene) : null;
                    MapPick? pick = board.Map.Units.Count > 0 ? new(board.Map.Units[0].Position, MapPickKind.Unit) : null;
                    Check(id, board, scene, camera, visual, pick);
                    if (id is "MV29" or "Mixed")
                    {
                        var changed = new MapScene(MapGallery.Changed(board.Map));
                        Check(id + "-changed", board, changed, camera, null, null);
                        Check(id + "-pan", board, changed, camera with { X = camera.X + 13, Y = camera.Y - 7 }, null, null);
                        Check(id + "-zoom", board, changed, camera with { Zoom = camera.Zoom * 1.25 }, null, null);
                        retained.RecoverResources();
                        Check(id + "-recovery", board, changed, camera, null, null);
                        var observer = new MapScene(board.Map with { Identity = board.Map.Identity with { Observer = new(2) }, Units = new([]), Cities = new([]) });
                        Check(id + "-observer", board, observer, camera, null, null);
                        if (experiment && id == "Mixed")
                        {
                            var local = board.Map;
                            var point = camera.Hit(960, 540, local) ?? new MapPoint(4, 2);
                            for (int edit = 0; edit < 8; edit++)
                            {
                                local = GpuMapExperiments.LocalChange(local, point);
                                Check(id + "-local-" + edit, board, new(local), camera, null, pick);
                            }
                        }
                    }
                }

                void Check(string id, GalleryBoard board, MapScene scene, MapCamera camera, VisualFrame? visual, MapPick? pick)
                {
                    int width = 1920 * density, height = 1080 * density;
                    var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
                    using var actual = SKSurface.Create(gl.Context, true, info);
                    using var fresh = SKSurface.Create(gl.Context, true, info);
                    using var currentGpu = experiment ? SKSurface.Create(gl.Context, true, info) : null;
                    using var expected = SKSurface.Create(info);
                    using var serial = new MapPainter(atlas, fonts, art, parallelForeground: false, packGroundTextures: false);
                    using var freshPainter = new MapPainter(atlas, fonts, art);
                    using var currentPainter = experiment ? new MapPainter(atlas, fonts, art) : null;
                    actual.Canvas.Scale(density);
                    fresh.Canvas.Scale(density);
                    expected.Canvas.Scale(density);
                    currentGpu?.Canvas.Scale(density);
                    long epoch = retained.SetScene(scene), referenceEpoch = serial.SetScene(scene), freshEpoch = freshPainter.SetScene(scene);
                    var gpu = MapGpuComposition.Draw(retained, gl.Context, actual.Canvas, epoch, camera, 1920, 1080, density, pick, board.Display, visual);
                    MapGpuComposition.Draw(freshPainter, gl.Context, fresh.Canvas, freshEpoch, camera, 1920, 1080, density, pick, board.Display, visual);
                    var cpu = serial.Draw(expected.Canvas, referenceEpoch, camera, 1920, 1080, density, pick, board.Display, visual);
                    if (currentPainter is not null)
                        MapGpuComposition.Draw(currentPainter, gl.Context, currentGpu!.Canvas, currentPainter.SetScene(scene), camera,
                            1920, 1080, density, pick, board.Display, visual, features: CommandBaseline);
                    NativeMapMatrix.Require(gpu.Error is null && cpu.Error is null && gpu.Tiles == cpu.Tiles && gpu.Objects == cpu.Objects &&
                        gpu.Labels == cpu.Labels && gpu.Effects == cpu.Effects, $"GPU semantic counts {id}");
                    using var read = Read(actual);
                    using var freshRead = Read(fresh);
                    bool exact = read.GetPixelSpan().SequenceEqual(freshRead.GetPixelSpan());
                    var freshDiff = Compare(read.GetPixelSpan(), freshRead.GetPixelSpan());
                    bool supported = MapGpuComposition.Features == MapGpuComposition.SupportedFeatures;
                    if (!exact && (!experiment || supported))
                    {
                        NativeMapMatrix.Encode(actual, Path.Combine(output, "failure-actual.png"));
                        NativeMapMatrix.Encode(fresh, Path.Combine(output, "failure-fresh.png"));
                        throw new InvalidOperationException($"GPU retained/fresh equality {id}: {JsonSerializer.Serialize(Compare(read.GetPixelSpan(), freshRead.GetPixelSpan()))}");
                    }
                    using var baseline = expected.PeekPixels();
                    var diff = Compare(read.GetPixelSpan(), baseline.GetPixelSpan());
                    using var currentRead = currentGpu is null ? null : Read(currentGpu);
                    var currentGpuDiff = currentRead is null ? null : Compare(read.GetPixelSpan(), currentRead.GetPixelSpan());
                    if (MapGpuComposition.Features == (MapGpuComposition.SupportedFeatures | MapGpuFeatures.Commands))
                    {
                        NativeMapMatrix.Require(exact, $"command stream history {id}");
                        NativeMapMatrix.Require(currentGpuDiff is null || currentGpuDiff.DifferentPixels == 0, $"command stream/current GPU pixels {id}: {currentGpuDiff}");
                    }
                    // Experimental backend acceptance declared in m2-renderer-options.md:
                    // resampling/AA may differ; semantic counts and same-backend history are exact.
                    if (supported) NativeMapMatrix.Require(diff.MeanChannelError <= 2 && diff.PixelsOver8 <= diff.Pixels * .10,
                        $"GPU/CPU declared raster tolerance {id}: {JsonSerializer.Serialize(diff)}");
                    results.Add(new { id, theme, density, width, height, diff, currentGpuDiff, freshGpuExact = exact, freshGpuDiff = freshDiff });
                    if (theme == "stock" && density == 1)
                    {
                        NativeMapMatrix.Encode(actual, Path.Combine(output, id + "-gpu.png"));
                        NativeMapMatrix.Encode(expected, Path.Combine(output, id + "-cpu.png"));
                    }
                }
                Console.WriteLine($"GPU matrix: {theme} {density}x complete");
            }
        var report = new
        {
            format = "openciv2-gpu-map-matrix/1",
            gl.Renderer,
            gl.Version,
            experiment,
            features = MapGpuComposition.Features.ToString(),
            commandBaseline = CommandBaseline.ToString(),
            scope = "Existing gallery, art replacements and change fixtures. Exact GPU retained/fresh and semantic comparisons; CPU/GPU pixel differences are reported, not silently accepted.",
            identity = JsonDocument.Parse(ApplicationIdentity.Read(typeof(GpuMapMatrix).Assembly)).RootElement,
            results
        };
        File.WriteAllText(Path.Combine(output, "gpu-matrix.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static SKBitmap Read(SKSurface surface)
    {
        using var image = surface.Snapshot();
        var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKImageInfo.PlatformColorType, SKAlphaType.Opaque));
        if (!surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
        {
            bitmap.Dispose();
            throw new InvalidOperationException("GPU readback failed");
        }
        return bitmap;
    }

    internal static Difference Compare(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected)
    {
        NativeMapMatrix.Require(actual.Length == expected.Length, "matching pixel dimensions");
        long different = 0, over8 = 0, total = 0;
        int maximum = 0;
        for (int i = 0; i < actual.Length; i += 4)
        {
            int pixelMax = 0;
            for (int c = 0; c < 3; c++)
            {
                int delta = Math.Abs(actual[i + c] - expected[i + c]);
                total += delta;
                pixelMax = Math.Max(pixelMax, delta);
            }
            if (pixelMax != 0) different++;
            if (pixelMax > 8) over8++;
            maximum = Math.Max(maximum, pixelMax);
        }
        return new(actual.Length / 4, different, over8, maximum, total / (actual.Length / 4d * 3));
    }
}
