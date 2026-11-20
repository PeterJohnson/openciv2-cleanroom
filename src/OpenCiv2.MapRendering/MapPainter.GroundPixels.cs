using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SkiaSharp;

namespace OpenCiv2.MapRendering;

public sealed partial class MapPainter
{
    private static bool AlignedGroundSupported =>
        OperatingSystem.IsLinux() && Sse2.IsSupported && BitConverter.IsLittleEndian;

    private static bool TryPaintAlignedGround(SKSurface surface, SKImage image, ReadOnlySpan<SKRect> sources,
        ReadOnlySpan<SKRotationScaleMatrix> transforms)
    {
        // P02 terrain chunks are opaque. This path matches the pinned Linux
        // Skia 8-bit atlas blend for a one-to-one, integer texel placement.
        if (!AlignedGroundSupported)
        {
            return false;
        }

        using var from = image.PeekPixels();
        using var to = surface.PeekPixels();
        if (from is null || to is null || from.ColorType != to.ColorType ||
            from.ColorType is not (SKColorType.Bgra8888 or SKColorType.Rgba8888) ||
            from.AlphaType != SKAlphaType.Premul || to.AlphaType != SKAlphaType.Opaque ||
            (from.RowBytes | to.RowBytes) % sizeof(uint) != 0)
        {
            return false;
        }

        using var sourceSpace = from.ColorSpace;
        using var targetSpace = to.ColorSpace;
        if (sourceSpace is not null || targetSpace is not null)
        {
            return false;
        }

        // The worker owns this fresh surface until the painter snapshots it.
        // No cached image or borrowed atlas is used as a write destination.
        var artwork = from.GetPixelSpan<uint>();
        var target = to.GetPixelSpan<uint>();
        int fromStride = from.RowBytes / sizeof(uint), toStride = to.RowBytes / sizeof(uint);
        for (int i = 0; i < sources.Length; i++)
        {
            var sprite = sources[i];
            int x = (int)transforms[i].TX, y = (int)transforms[i].TY;
            int left = Math.Max(0, x), top = Math.Max(0, y);
            int width = Math.Min(to.Width, x + (int)sprite.Width) - left;
            int bottom = Math.Min(to.Height, y + (int)sprite.Height);
            if (width <= 0 || top >= bottom)
            {
                continue;
            }

            int src = ((int)sprite.Top + top - y) * fromStride + (int)sprite.Left + left - x;
            int dst = top * toStride + left;
            for (int row = top; row < bottom; row++, src += fromStride, dst += toStride)
            {
                BlendGroundRow(artwork.Slice(src, width), target.Slice(dst, width));
            }
        }
        return true;
    }

    private static void BlendGroundRow(ReadOnlySpan<uint> source, Span<uint> target)
    {
        int i = Avx2.IsSupported ? BlendGroundWide(source, target) : BlendGroundBaseline(source, target);
        const uint redBlue = 0x00ff00ff, green = 0x0000ff00, opaque = 0xff000000;
        for (; i < source.Length; i++)
        {
            uint over = source[i], under = target[i], inverse = 255 - (over >> 24);
            uint rb = (((under & redBlue) * inverse + redBlue) >> 8) & redBlue;
            uint g = (((under >> 8) & 255) * inverse + 255) & green;
            target[i] = ((over & ~opaque) + rb + g) | opaque;
        }
    }

    private static int BlendGroundWide(ReadOnlySpan<uint> source, Span<uint> target)
    {
        const uint redBlue = 0x00ff00ff, green = 0x0000ff00, opaque = 0xff000000;
        var rbMask = Vector256.Create(redBlue);
        var gMask = Vector256.Create(green);
        var full = Vector256.Create(255u);
        var aMask = Vector256.Create(opaque);
        ref uint input = ref MemoryMarshal.GetReference(source);
        ref uint output = ref MemoryMarshal.GetReference(target);
        int i = 0;
        for (; i <= source.Length - Vector256<uint>.Count; i += Vector256<uint>.Count)
        {
            var over = Vector256.LoadUnsafe(ref input, (nuint)i);
            if (over == Vector256<uint>.Zero)
            {
                continue;
            }

            var inverse = full - (over >> 24);
            if (inverse == Vector256<uint>.Zero)
            {
                over.StoreUnsafe(ref output, (nuint)i);
                continue;
            }
            var under = Vector256.LoadUnsafe(ref output, (nuint)i);
            // Round the destination contribution as (value * inverse + 255) >> 8.
            // Each 16-bit lane remains independent in the packed red and blue pair.
            var rb = (((under & rbMask) * inverse + rbMask) >> 8) & rbMask;
            var g = (((under >> 8) & full) * inverse + full) & gMask;
            (((over & ~aMask) + rb + g) | aMask).StoreUnsafe(ref output, (nuint)i);
        }
        return i;
    }

    private static int BlendGroundBaseline(ReadOnlySpan<uint> source, Span<uint> target)
    {
        // Portable x64 Native AOT builds have SSE2. Unpack two RGBA pixels
        // at a time so the exact blend needs only 16-bit multiplication.
        var full = Vector128.Create((ushort)255);
        var opaque = Vector128.Create(0xff000000u);
        var zero = Vector128<byte>.Zero;
        ref uint input = ref MemoryMarshal.GetReference(source);
        ref uint output = ref MemoryMarshal.GetReference(target);
        int i = 0;
        for (; i <= source.Length - Vector128<uint>.Count; i += Vector128<uint>.Count)
        {
            var over = Vector128.LoadUnsafe(ref input, (nuint)i);
            if (over == Vector128<uint>.Zero)
            {
                continue;
            }

            if ((over & opaque) == opaque)
            {
                over.StoreUnsafe(ref output, (nuint)i);
                continue;
            }
            var under = Vector128.LoadUnsafe(ref output, (nuint)i).AsByte();
            var low = BlendGroundChannels(Sse2.UnpackLow(over.AsByte(), zero).AsUInt16(),
                Sse2.UnpackLow(under, zero).AsUInt16(), full);
            var high = BlendGroundChannels(Sse2.UnpackHigh(over.AsByte(), zero).AsUInt16(),
                Sse2.UnpackHigh(under, zero).AsUInt16(), full);
            (Sse2.PackUnsignedSaturate(low.AsInt16(), high.AsInt16())
                .AsUInt32() | opaque).StoreUnsafe(
                    ref output,
                    (nuint)i);
        }
        return i;
    }

    private static Vector128<ushort> BlendGroundChannels(
        Vector128<ushort> over,
        Vector128<ushort> under,
        Vector128<ushort> full)
    {
        // Replicate each pixel's alpha into its four channels, then round
        // the destination contribution as (value * (255 - alpha) + 255) >> 8.
        var alpha = Sse2.ShuffleHigh(Sse2.ShuffleLow(over, 0xff), 0xff);
        return over + ((Sse2.MultiplyLow(under, full - alpha) + full) >> 8);
    }
}
