using SkiaSharp;

// Shared native/WASM GPU readback and declared pixel comparisons.
internal static class GpuPixelChecks
{
    internal sealed record Difference(long Pixels, long DifferentPixels, long PixelsOver8, int MaxChannelError, double MeanChannelError);

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

    internal delegate Difference Comparator(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected);
    // Test hosts may accelerate bulk pixel arithmetic; production never references this helper.
    internal static Comparator? AcceleratedCompare { get; set; }
    internal static Difference Compare(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected) =>
        AcceleratedCompare is { } compare ? compare(actual, expected) : CompareScalar(actual, expected);

    internal static Difference CompareScalar(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected)
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
