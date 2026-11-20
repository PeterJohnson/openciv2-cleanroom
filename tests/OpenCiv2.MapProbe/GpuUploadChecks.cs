using System.Text.Json;
using OpenCiv2.App.Map;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static class GpuUploadChecks
{
    public static void Run(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        using var gl = new MapProbeGlContext(profile: true);
        var board = MapGallery.Open("Mixed");
        var results = new List<object>();
        bool passed = true;
        foreach (int density in new[] { 1, 2 })
        {
            var info = new SKImageInfo(1920 * density, 1080 * density, SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
            using var target = SKSurface.Create(gl.Context, true, info);
            using var actual = new MapPainter(atlas, fonts);
            var world = board.Map;
            var camera = NativeMapMatrix.BoardCamera(world);
            for (int i = 0; i < 10; i++)
            {
                if (i is >= 1 and <= 4) camera = camera with { X = camera.X + 3, Y = camera.Y - 2 };
                if (i == 5) camera = camera with { Zoom = camera.Zoom * 1.25 };
                if (i >= 6) world = MapGallery.Changed(world);
                var scene = new MapScene(world);
                long epoch = actual.SetScene(scene);
                var before = gl.Profiler!.Counts;
                target.Canvas.Save();
                target.Canvas.Scale(density);
                actual.Draw(target.Canvas, epoch, camera, 1920, 1080, density, null, board.Display);
                target.Canvas.Restore();
                gl.Context.Flush(submit: true, synchronous: true);
                var calls = MapGlProfiler.Delta(before, gl.Profiler.Counts);
                using var expected = SKSurface.Create(info);
                using var fresh = new MapPainter(atlas, fonts);
                expected.Canvas.Scale(density);
                fresh.Draw(expected.Canvas, fresh.SetScene(scene), camera, 1920, 1080, density, null, board.Display);
                using var pixels = GpuMapMatrix.Read(target);
                using var reference = expected.PeekPixels();
                var diff = GpuMapMatrix.Compare(pixels.GetPixelSpan(), reference.GetPixelSpan());
                bool exact = diff.DifferentPixels == 0;
                passed &= exact;
                results.Add(new { density, frame = i, exact, calls, diff });
                Console.WriteLine($"CPU-to-GPU upload {density}x frame {i}: {diff.DifferentPixels} different pixels; {calls.UploadBytes} upload bytes");
            }
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new { passed, gl.Renderer, results }, new JsonSerializerOptions { WriteIndented = true }));
        if (!passed) throw new InvalidOperationException("CPU-composed GPU presentation differs from the fresh CPU pixels");
    }
}
