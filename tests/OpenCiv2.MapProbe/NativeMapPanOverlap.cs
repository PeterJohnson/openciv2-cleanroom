using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static class NativeMapPanOverlap
{
    private const int BlockSize = 64;
    private sealed record Sample(int Dx, int Dy, long OverlapPixels, int DifferentPixels, int DirtyBlocks, int DifferentRows, int DifferentColumns);

    public static void Run(AssetAtlas atlas, PinnedFonts fonts, string receipt, string output)
    {
        using var input = JsonDocument.Parse(File.ReadAllBytes(receipt));
        NativeMapMatrix.Require(input.RootElement.GetProperty("format").GetString() == "openciv2-native-map-input-timing/1", "native timing input");
        var board = MapGallery.Open("Mixed");
        var comparisons = new List<object>();
        foreach (var series in input.RootElement.GetProperty("results").EnumerateArray())
        {
            if (series.GetProperty("mode").GetString() != "pan") continue;
            int width = series.GetProperty("width").GetInt32(), height = series.GetProperty("height").GetInt32();
            double density = series.GetProperty("density").GetDouble();
            var map = board.Map with { Flat = series.GetProperty("flat").GetBoolean() };
            var frames = series.GetProperty("samples");
            foreach (bool captions in new[] { true, false })
            {
                var world = captions ? map : map with { Cities = new(map.Cities.Select(city => city with { Name = "" })) };
                comparisons.Add(new { width, height, density, world.Flat, captions, samples = CompareSeries(world, board.Display, frames, width, height, density) });
            }
        }
        NativeMapMatrix.Require(comparisons.Count > 0, "native receipt includes a pan series");
        var report = new
        {
            format = "openciv2-native-pan-overlap/1",
            receiptSha256 = NativeMapMatrix.FileHash(receipt),
            identity = JsonDocument.Parse(ApplicationIdentity.Read(typeof(NativeMapPanOverlap).Assembly)).RootElement,
            blockSize = BlockSize,
            comparisons
        };
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

        List<Sample> CompareSeries(MapObservation world, MapDisplay display, JsonElement frames, int width, int height, double density)
        {
            using var painter = new MapPainter(atlas, fonts, parallelForeground: false, packGroundTextures: false);
            var info = new SKImageInfo(width, height);
            using var surface = SKSurface.Create(info);
            using var previousPixels = new SKBitmap(info);
            surface.Canvas.Scale((float)density);
            long epoch = painter.SetScene(new(world));
            MapCamera? previous = null;
            var samples = new List<Sample>();
            foreach (var frame in frames.EnumerateArray())
            {
                var camera = new MapCamera(frame.GetProperty("x").GetDouble(), frame.GetProperty("y").GetDouble(), frame.GetProperty("zoom").GetDouble());
                painter.InvalidateFrame();
                var rendered = painter.Draw(surface.Canvas, epoch, camera, width / density, height / density, density, null, display);
                NativeMapMatrix.Require(rendered.Error is null, "pan overlap renders successfully");
                double worldWidth = world.Width * MapCamera.HalfWidth * camera.Zoom;
                if (previous is { } prior && TryMeasureShift(prior, camera, density, world.Flat ? 0 : worldWidth, out int dx, out int dy))
                {
                    using var from = previousPixels.PeekPixels();
                    using var to = surface.PeekPixels();
                    samples.Add(ComparePixels(from, to, dx, dy));
                }
                NativeMapMatrix.Require(surface.ReadPixels(info, previousPixels.GetPixels(), previousPixels.RowBytes, 0, 0), "pan overlap copies the rendered viewport");
                previous = camera;
            }
            return samples;
        }
    }

    private static bool TryMeasureShift(MapCamera before, MapCamera after, double density, double period, out int dx, out int dy)
    {
        double x = (after.X - before.X) * density;
        double y = (after.Y - before.Y) * density;
        if (period > 0) x -= Math.Round(x / (period * density)) * period * density;
        dx = (int)Math.Round(x);
        dy = (int)Math.Round(y);
        return before.Zoom == after.Zoom && (dx != 0 || dy != 0) && Math.Abs(dx - x) < 1e-7 && Math.Abs(dy - y) < 1e-7;
    }

    private static Sample ComparePixels(SKPixmap source, SKPixmap target, int dx, int dy)
    {
        int left = Math.Min(target.Width, Math.Max(0, dx)), top = Math.Min(target.Height, Math.Max(0, dy));
        int right = Math.Max(left, target.Width + Math.Min(0, dx)), bottom = Math.Max(top, target.Height + Math.Min(0, dy));
        int width = right - left;
        if (width == 0 || bottom == top) return new(dx, dy, 0, 0, 0, 0, 0);
        int blockColumns = (target.Width + BlockSize - 1) / BlockSize;
        var dirty = new bool[blockColumns * ((target.Height + BlockSize - 1) / BlockSize)];
        var columns = new bool[target.Width];
        var before = source.GetPixelSpan<uint>();
        var after = target.GetPixelSpan<uint>();
        int changed = 0, rows = 0;
        for (int y = top; y < bottom; y++)
        {
            int sourceStart = (y - dy) * (source.RowBytes / 4) + left - dx;
            int targetStart = y * (target.RowBytes / 4) + left;
            var former = before.Slice(sourceStart, width);
            var current = after.Slice(targetStart, width);
            if (former.SequenceEqual(current)) continue;
            rows++;
            for (int i = 0; i < width; i++)
            {
                if (former[i] == current[i]) continue;
                int column = left + i;
                columns[column] = true;
                dirty[y / BlockSize * blockColumns + column / BlockSize] = true;
                changed++;
            }
        }
        return new(dx, dy, (long)width * (bottom - top), changed, dirty.Count(value => value), rows, columns.Count(value => value));
    }
}
