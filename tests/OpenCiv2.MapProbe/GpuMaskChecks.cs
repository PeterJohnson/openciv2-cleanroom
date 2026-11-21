using OpenCiv2.App.Map;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static class GpuMaskChecks
{
    internal static object Run(GRContext context, bool modulated, bool enforce = true)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(13, 11, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var white = new SKBitmap(bitmap.Info);
        using var pixels = bitmap.PeekPixels();
        using var whitePixels = white.PeekPixels();
        var bytes = pixels.GetPixelSpan<byte>();
        var maskBytes = whitePixels.GetPixelSpan<byte>();
        byte[] alphas = [0, 1, 2, 17, 63, 127, 128, 192, 254, 255];
        for (int i = 0; i < bytes.Length; i += 4)
        {
            // Arbitrary RGB under zero alpha must never leak into a colored mask.
            bytes[i] = unchecked((byte)(i * 37)); bytes[i + 1] = unchecked((byte)(i * 11)); bytes[i + 2] = unchecked((byte)(i * 73));
            bytes[i + 3] = alphas[(i / 4) % alphas.Length];
            maskBytes[i] = maskBytes[i + 1] = maskBytes[i + 2] = 255;
            maskBytes[i + 3] = bytes[i + 3];
        }
        bitmap.SetImmutable(); white.SetImmutable();
        using var image = SKImage.FromBitmap(bitmap);
        using var mask = SKImage.FromBitmap(white);
        var atlas = new MapGpuSpriteAtlas { StableLayout = true, ReuseBatchStorage = true, ModulatedMasks = modulated };
        var key = new MapGpuSpriteAtlas.Key("authored-alpha-mask", 13, 11, null);
        var selected = modulated ? mask : image;
        atlas.BeginPreparation(); atlas.Collect(key, selected); atlas.FinishPreparation();
        long residentBytes = atlas.Bytes;
        int residentPages = atlas.LayoutCount;
        var colors = MapArtCatalog.Stock.Factions.Select(faction => new SKColor(0xff000000 | faction.Rgb))
            .Concat(new SKColor[] { new(0x00123456), new(0x01808080), new(0x7f29a9df), new(0xfeff0101) }).ToArray();
        var rows = new List<object>();
        int failures = 0;
        try
        {
            foreach (var color in colors)
                foreach (var (scale, dx, dy) in new[] { (1f, 0f, 0f), (1.37f, .25f, .5f) })
                {
                    var info = new SKImageInfo(48, 48, SKColorType.Rgba8888, SKAlphaType.Premul);
                    using var actual = SKSurface.Create(context, true, info);
                    using var reference = SKSurface.Create(context, true, info);
                    actual.Canvas.Clear(SKColors.Transparent); reference.Canvas.Clear(SKColors.Transparent);
                    var box = SKRect.Create(8 + dx, 8 + dy, 13 * scale, 11 * scale);
                    atlas.BeginFrame();
                    atlas.Draw(actual.Canvas, key, selected, box, new(SKFilterMode.Linear), null, color);
                    using var filter = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn);
                    using var ink = new SKPaint { ColorFilter = filter };
                    reference.Canvas.DrawImage(image, box, new SKSamplingOptions(SKFilterMode.Linear), ink);
                    // Preserve ordered translucent overlap within one atlas flush.
                    var other = new SKColor(0x6f5982d1);
                    box.Offset(3, 2);
                    atlas.Draw(actual.Canvas, key, selected, box, new(SKFilterMode.Linear), null, other);
                    atlas.Flush();
                    using var secondFilter = SKColorFilter.CreateBlendMode(other, SKBlendMode.SrcIn);
                    ink.ColorFilter = secondFilter;
                    reference.Canvas.DrawImage(image, box, new SKSamplingOptions(SKFilterMode.Linear), ink);
                    using var actualPixels = new SKBitmap(info);
                    using var expectedPixels = new SKBitmap(info);
                    NativeMapMatrix.Require(actual.ReadPixels(info, actualPixels.GetPixels(), actualPixels.RowBytes, 0, 0) &&
                        reference.ReadPixels(info, expectedPixels.GetPixels(), expectedPixels.RowBytes, 0, 0), "mask readback");
                    var a = actualPixels.GetPixelSpan(); var b = expectedPixels.GetPixelSpan();
                    int maximum = 0, alphaMaximum = 0;
                    long different = 0;
                    for (int i = 0; i < a.Length; i++)
                    {
                        int error = Math.Abs(a[i] - b[i]);
                        maximum = Math.Max(maximum, error);
                        if ((i & 3) == 3) alphaMaximum = Math.Max(alphaMaximum, error);
                        if (error != 0) different++;
                    }
                    bool withinBounds = alphaMaximum == 0 && maximum <= 1;
                    if (!withinBounds) failures++;
                    if (enforce) NativeMapMatrix.Require(withinBounds,
                        $"mask alpha equals direct filtering; premultiplied RGB differs by at most one rounding level: {color}, {scale}, RGB {maximum}, alpha {alphaMaximum}");
                    NativeMapMatrix.Require(atlas.Bytes == residentBytes && atlas.LayoutCount == residentPages && atlas.Admissions == 0,
                        "recoloring a resident mask allocates no page or slot");
                    rows.Add(new
                    {
                        color = (uint)color,
                        scale,
                        dx,
                        dy,
                        maximum,
                        alphaMaximum,
                        differentChannels = different,
                        atlas.Bytes,
                        pages = atlas.LayoutCount
                    });
                }
        }
        finally { atlas.Clear(); }
        return new { modulated, stableLayout = true, enforced = enforce, failures, residentBytes, residentPages, rows };
    }
}
