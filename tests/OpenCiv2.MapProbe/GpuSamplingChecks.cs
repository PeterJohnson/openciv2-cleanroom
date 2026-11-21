using System.Runtime.InteropServices;
using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

// Isolate P02 nearest-cache sampling from map preparation and presentation.
internal static class GpuSamplingChecks
{
    internal static void Run(GRContext context, AssetAtlas atlas, PinnedFonts fonts, string output, string fixtures)
    {
        Directory.CreateDirectory(output);
        using var sampler = new MapGpuNearest();
        var primitive = new List<object>();
        int regressions = 0, failures = 0;
        foreach (int side in new[] { 260, 520 })
        {
            var pixels = new byte[side * side * 4];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    int i = (y * side + x) * 4;
                    pixels[i] = (byte)(x & 255); pixels[i + 1] = (byte)(y & 255);
                    pixels[i + 2] = (byte)((x >> 8) * 64 + (y >> 8) * 16); pixels[i + 3] = 255;
                }
            using var bitmap = new SKBitmap(new SKImageInfo(side, side, SKColorType.Rgba8888, SKAlphaType.Opaque));
            Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
            using var image = SKImage.FromBitmap(bitmap);
            foreach (float scale in new[] { 1f, 1.2499f, 1.25f, 1.2501f, 2.4999f })
                foreach (float bias in new[] { 0f, -.001f })
                    foreach (string mode in new[] { "image", "pixel-centers" })
                    {
                        const int width = 720, height = 560;
                        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
                        using var cpu = SKSurface.Create(info);
                        using var gpu = SKSurface.Create(context, true, info);
                        var source = SKRect.Create(side / 130f + bias, side / 130f + bias, side * 128 / 130f, side * 128 / 130f);
                        var target = SKRect.Create(23, 17, source.Width * scale, source.Height * scale);
                        foreach (var surface in new[] { cpu, gpu })
                        {
                            surface.Canvas.Clear(SKColors.Black);
                            if (surface == gpu && mode == "pixel-centers") sampler.Draw(surface.Canvas, image, source, target);
                            else surface.Canvas.DrawImage(image, source, target, new SKSamplingOptions(SKFilterMode.Nearest));
                        }
                        using var cpuRead = GpuPixelChecks.Read(cpu); using var gpuRead = GpuPixelChecks.Read(gpu);
                        var difference = GpuPixelChecks.Compare(gpuRead.GetPixelSpan(), cpuRead.GetPixelSpan());
                        bool regression = mode == "pixel-centers" && bias == 0 && scale is 1.2499f or 2.4999f;
                        if (regression)
                        {
                            regressions++;
                            if (difference.DifferentPixels != 0) failures++;
                        }
                        primitive.Add(new { side, scale, bias, mode, regression, difference });
                        if (scale == 1.2499f && bias == 0)
                        {
                            NativeMapMatrix.Encode(cpu, Path.Combine(output, $"primitive-{side}-{mode}-cpu.png"));
                            NativeMapMatrix.Encode(gpu, Path.Combine(output, $"primitive-{side}-{mode}-gpu.png"));
                        }
                        Console.WriteLine($"Sampling primitive: {side}/{scale}/{bias}/{mode}, different={difference.DifferentPixels}");
                    }
        }

        int coverage = CoverageChecks(context, sampler);
        var (native, classic) = NativeMapMatrix.OverrideChecks(fixtures);
        var rows = new List<object>();
        var board = MapGallery.Open("Mixed");
        var scene = new MapScene(board.Map);
        foreach (var (theme, art) in new (string, MapArtSelection?)[] { ("stock", null), ("native", native), ("classic", classic) })
            foreach (double zoom in new[] { 1.2499, 1.25, 2.4999 })
            {
                var camera = NativeMapMatrix.BoardCamera(board.Map) with { Zoom = zoom };
                var info = new SKImageInfo(960, 540, SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
                using var cpu = SKSurface.Create(info); using var gpu = SKSurface.Create(context, true, info);
                using var cpuPainter = new MapPainter(atlas, fonts, art, parallelForeground: false, packGroundTextures: false);
                using var gpuPainter = new MapPainter(atlas, fonts, art);
                var cpuStats = cpuPainter.Draw(cpu.Canvas, cpuPainter.SetScene(scene), camera, 960, 540, 1, null, board.Display);
                var gpuStats = MapGpuComposition.Draw(gpuPainter, context, gpu.Canvas, gpuPainter.SetScene(scene), camera, 960, 540, 1, null, board.Display);
                NativeMapMatrix.Require(cpuStats.Error is null && gpuStats.Error is null && cpuStats.Tiles == gpuStats.Tiles && cpuStats.Objects == gpuStats.Objects, "sampling map semantics");
                using var cpuRead = GpuPixelChecks.Read(cpu); using var gpuRead = GpuPixelChecks.Read(gpu);
                var difference = GpuPixelChecks.Compare(gpuRead.GetPixelSpan(), cpuRead.GetPixelSpan());
                bool withinBounds = difference.MeanChannelError <= 2 && difference.PixelsOver8 <= difference.Pixels * .1;
                if (!withinBounds) failures++;
                string name = theme + "-" + zoom.ToString(System.Globalization.CultureInfo.InvariantCulture);
                NativeMapMatrix.Encode(cpu, Path.Combine(output, name + "-cpu.png"));
                NativeMapMatrix.Encode(gpu, Path.Combine(output, name + "-gpu.png"));
                rows.Add(new { theme, zoom, difference, withinBounds });
                Console.WriteLine($"Sampling map: {name}, mean={difference.MeanChannelError}, bounds={withinBounds}");
            }
        File.WriteAllText(Path.Combine(output, "sampling.json"), JsonSerializer.Serialize(new
        {
            identity = JsonDocument.Parse(ApplicationIdentity.Read(typeof(GpuSamplingChecks).Assembly)).RootElement,
            features = MapGpuComposition.Features.ToString(),
            regressions,
            coverage,
            failures,
            primitive,
            rows
        }, new JsonSerializerOptions { WriteIndented = true }));
        NativeMapMatrix.Require(failures == 0, "GPU sampling regressions and map CPU bounds");
    }

    private static int CoverageChecks(GRContext context, MapGpuNearest sampler)
    {
        // Solid adjacent chunks isolate coverage from texel/color differences.
        // Include half-pixel edges, clipping, negative origins and host scaling.
        using var redBitmap = new SKBitmap(4, 4); redBitmap.Erase(SKColors.Red);
        using var greenBitmap = new SKBitmap(4, 4); greenBitmap.Erase(SKColors.Lime);
        using var red = SKImage.FromBitmap(redBitmap); using var green = SKImage.FromBitmap(greenBitmap);
        var info = new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Opaque);
        int checks = 0;
        foreach (float density in new[] { 1f, 1.5f, 2f })
            foreach (float offset in new[] { -13.5f, -.5f, 0f, .25f, .5f, .75f, 17.5f })
            {
                using var cpu = SKSurface.Create(info); using var gpu = SKSurface.Create(context, true, info);
                foreach (var surface in new[] { cpu, gpu })
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(SKColors.Black);
                    canvas.ClipRect(new(5, 5, 50, 49));
                    canvas.Translate(7.25f, 3.5f);
                    canvas.Scale(density);
                    float edge = 13 + offset;
                    foreach (var image in new[] { red, green })
                    {
                        var target = SKRect.Create(edge, 4 + offset, 17.499f, 12.5f);
                        if (surface == gpu) sampler.Draw(canvas, image, new(0, 0, 4, 4), target);
                        else canvas.DrawImage(image, new(0, 0, 4, 4), target, new SKSamplingOptions(SKFilterMode.Nearest));
                        edge = target.Right;
                    }
                }
                using var cpuRead = GpuPixelChecks.Read(cpu); using var gpuRead = GpuPixelChecks.Read(gpu);
                NativeMapMatrix.Require(GpuPixelChecks.Compare(gpuRead.GetPixelSpan(), cpuRead.GetPixelSpan()).DifferentPixels == 0,
                    $"GPU chunk coverage matches CPU at density {density}, offset {offset}");
                checks++;
            }
        return checks;
    }
}
