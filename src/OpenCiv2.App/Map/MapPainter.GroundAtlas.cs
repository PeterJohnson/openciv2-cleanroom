using SkiaSharp;

namespace OpenCiv2.App.Map;

internal sealed partial class MapPainter
{
    private const int GroundAtlasSide = 1024;
    internal long PackedGroundBytes => retainedGroundAtlas?.Image is null ? 0 : GroundAtlasSide * GroundAtlasSide * 4L;
    private GroundAtlas? retainedGroundAtlas;
    private (double Tier, bool HalfSize) retainedGroundAtlasKey;
    private readonly record struct PackedGroundSprite(SKRect Source, SKPointI Offset, int OriginalWidth);

    private GroundAtlas? PackRetainedGround(IReadOnlyDictionary<string, SKImage> images, double tier, bool halfSize)
    {
        var key = (tier, halfSize);
        if (retainedGroundAtlasKey != key) ClearRetainedGroundAtlas();
        bool extending = retainedGroundAtlas is not null;
        retainedGroundAtlasKey = key;
        retainedGroundAtlas ??= new(halfSize, immutableBacking: true);
        if (retainedGroundAtlas.Include(images)) return retainedGroundAtlas;

        // The resident texture may fill after visiting more of the map.
        // Retry with only this batch's sources before falling back to separate images.
        ClearRetainedGroundAtlas();
        if (!extending) return null;
        retainedGroundAtlas = new(halfSize, immutableBacking: true);
        if (retainedGroundAtlas.Include(images)) return retainedGroundAtlas;
        ClearRetainedGroundAtlas();
        return null;
    }

    private void ClearRetainedGroundAtlas()
    {
        retainedGroundAtlas?.Dispose();
        retainedGroundAtlas = null;
    }

    private sealed class GroundAtlas(bool halfSize = false, bool immutableBacking = false) : IDisposable
    {
        private SKBitmap? pixels;
        private readonly Dictionary<string, PackedGroundSprite> sources = new(StringComparer.Ordinal);
        private int left, top, shelf;
        private bool dirty;
        public SKImage? Image { get; private set; }
        public IReadOnlyDictionary<string, PackedGroundSprite> Sources => sources;

        public bool Include(IReadOnlyDictionary<string, SKImage> images)
        {
            if (Image is not null && !dirty && images.Keys.All(sources.ContainsKey)) return true;
            pixels ??= new();
            if (pixels.GetPixels() == IntPtr.Zero)
            {
                var info = new SKImageInfo(GroundAtlasSide, GroundAtlasSide, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
                var flags = Image is null ? SKBitmapAllocFlags.ZeroPixels : SKBitmapAllocFlags.None;
                if (!pixels.TryAllocPixels(info, flags) ||
                    Image is { } existing && !existing.ReadPixels(info, pixels.GetPixels(), pixels.RowBytes))
                    return false;
            }
            using var destination = pixels.PeekPixels();
            if (destination is null) return false;
            foreach (var (binding, image) in images)
            {
                if (sources.ContainsKey(binding)) continue;
                using var filtered = halfSize ? SampleAtHalfSize(image) : null;
                if (halfSize && filtered is null) return false;
                using var source = (filtered ?? image).PeekPixels();
                if (source is null) return false;
                var crop = GroundSpriteBounds(source);
                int paddedWidth = crop.Width + 2, paddedHeight = crop.Height + 2;
                if (paddedWidth > GroundAtlasSide) return false;
                if (left + paddedWidth > GroundAtlasSide)
                {
                    top += shelf;
                    left = shelf = 0;
                }
                if (top + paddedHeight > GroundAtlasSide) return false;
                var box = SKRectI.Create(left + 1, top + 1, crop.Width, crop.Height);
                if (!CopyWithGutter(source, destination, crop, box)) return false;
                sources.Add(binding, new(box, new(crop.Left, crop.Top), source.Width));
                dirty = true;
                shelf = Math.Max(shelf, paddedHeight);
                left += paddedWidth;
            }
            SKImage? snapshot;
            if (immutableBacking)
            {
                // The retained image owns the finished pixels. Allocate new
                // staging only if another batch needs additional sources.
                pixels.SetImmutable();
                snapshot = SKImage.FromBitmap(pixels);
                pixels.Dispose();
                pixels = null;
            }
            else snapshot = SKImage.FromPixelCopy(destination);
            Image?.Dispose();
            Image = snapshot;
            dirty = Image is null;
            return Image is not null;
        }

        private static SKImage? SampleAtHalfSize(SKImage image)
        {
            // Use the same linear atlas sampler against transparent pixels.
            // Keep the sampled pixels premultiplied for aligned compositing.
            using var raster = SKSurface.Create(new SKImageInfo(image.Width / 2, image.Height / 2, image.ColorType, SKAlphaType.Premul));
            if (raster is null) return null;
            raster.Canvas.Clear(SKColors.Transparent);
            raster.Canvas.DrawAtlas(image, [SKRect.Create(image.Width, image.Height)], [new(.5f, 0, 0, 0)], Sampling);
            return raster.Snapshot();
        }

        public void Dispose()
        {
            Image?.Dispose();
            pixels?.Dispose();
        }
    }

    private bool CanPackGroundTextures(IReadOnlyDictionary<string, SKImage> images, double tier) =>
        images.Count >= 2 && images.All(entry => entry.Value.ColorType == SKImageInfo.PlatformColorType &&
            entry.Value.AlphaType == SKAlphaType.Premul && textures.Logical(entry.Key).X * tier <= entry.Value.Width);

    private bool CanSampleGroundAtHalfSize(IReadOnlyDictionary<string, SKImage> images, GroundPlan[] plans)
    {
        // Half-size sampling is independent of placement only for exact
        // one-to-two source registration at integral destination positions.
        foreach (var (key, image) in images)
        {
            var logical = textures.Logical(key);
            if (image.Width != logical.X || image.Height != logical.Y || ((image.Width | image.Height) & 1) != 0)
                return false;
            using var space = image.ColorSpace;
            if (space is not null) return false;
        }
        foreach (var plan in plans)
            foreach (var positions in plan.Groups.Values)
                for (int i = 0; i < positions.Count; i++)
                {
                    var position = positions[i];
                    if (position.X != MathF.Truncate(position.X) || position.Y != MathF.Truncate(position.Y))
                        return false;
                }
        return true;
    }

    private static SKRectI GroundSpriteBounds(SKPixmap source)
    {
        var pixels = source.GetPixelSpan();
        int width = source.Width, height = source.Height, stride = source.RowBytes;
        int left = width, top = height, right = -1, bottom = -1;
        for (int y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * stride, width * 4);
            for (int x = 0; x < width; x++)
            {
                if (row[x * 4 + 3] == 0) continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }
        if (right < 0) return SKRectI.Create(1, 1);
        // Leave transparent source texels around the visible region for
        // filtering and edge coverage. The offset restores the original anchor.
        const int margin = 2;
        return new(Math.Max(0, left - margin), Math.Max(0, top - margin),
            Math.Min(width, right + 1 + margin), Math.Min(height, bottom + 1 + margin));
    }

    private static bool CopyWithGutter(SKPixmap source, SKPixmap destination, SKRectI crop, SKRectI box)
    {
        // Duplicate the edge texels so a linear sample at the limit of a
        // sprite still sees the same clamped color as an independent image.
        int right = box.Width - 1, bottom = box.Height - 1;
        var info = destination.Info;
        int stride = destination.RowBytes;
        return Copy(0, 0, box.Width, box.Height, 0, 0)
            && Copy(0, 0, box.Width, 1, 0, -1)
            && Copy(0, bottom, box.Width, 1, 0, box.Height)
            && Copy(0, 0, 1, box.Height, -1, 0)
            && Copy(right, 0, 1, box.Height, box.Width, 0)
            && Copy(0, 0, 1, 1, -1, -1)
            && Copy(right, 0, 1, 1, box.Width, -1)
            && Copy(0, bottom, 1, 1, -1, box.Height)
            && Copy(right, bottom, 1, 1, box.Width, box.Height);

        bool Copy(int x, int y, int width, int height, int dx, int dy) =>
            source.ReadPixels(info.WithSize(width, height), destination.GetPixels(box.Left + dx, box.Top + dy),
                stride, crop.Left + x, crop.Top + y);
    }

    private void PaintPackedGround(SKSurface surface, GroundPlan plan, GroundAtlas packed)
    {
        var canvas = surface.Canvas;
        canvas.Clear(Background);
        if (plan.Groups.Count == 0) return;
        float zoom = (float)plan.Chunk.Camera.Zoom;
        int count = plan.Groups.Values.Sum(positions => positions.Count);
        var rects = new SKRect[count];
        var transforms = new SKRotationScaleMatrix[count];
        bool integerSampling = true;
        int offset = 0;
        foreach (var (binding, instances) in plan.Groups.OrderBy(group => group.Key.Layer)
                     .ThenBy(group => group.Key.Row).ThenBy(group => group.Key.Piece))
        {
            var sprite = packed.Sources[binding.Binding];
            float scale = zoom * textures.Logical(binding.Binding).X / sprite.OriginalWidth;
            for (int i = 0; i < instances.Count; i++, offset++)
            {
                float x = instances[i].X + sprite.Offset.X * scale, y = instances[i].Y + sprite.Offset.Y * scale;
                integerSampling &= scale == 1 && x == MathF.Truncate(x) && y == MathF.Truncate(y);
                rects[offset] = sprite.Source;
                transforms[offset] = new(scale, 0, x, y);
            }
        }
        var image = packed.Image!;
        if (integerSampling && TryPaintAlignedGround(surface, image, rects, transforms))
        {
            Interlocked.Increment(ref alignedGroundChunks);
            return;
        }
        canvas.DrawAtlas(image, rects, transforms, Sampling);
    }
}
