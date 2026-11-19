using OpenCiv2.Contracts;
using SkiaSharp;

namespace OpenCiv2.App.Map;

internal sealed partial class MapPainter
{
    private SKSurface? terrainRaster, viewportScratch;
    private SKImage? terrainImage;
    private FrameKey terrainKey;
    private HashSet<MapPoint>? settledUpdates;
    private sealed record TerrainFrame(SKImage Image, GroundWork Work, long Pixels, bool Scrolled, bool SeedBands = false);

    private TerrainFrame? PrepareSettledTerrain(MapScene scene, FrameKey frame, SKImageInfo info, bool reuseAtlas)
    {
        if (terrainKey != frame || settledUpdates is null)
        {
            ClearTerrainFrame();
            if (!CanPatchSettledTerrain(scene.Map, frame, info)) return null;
        }
        terrainRaster ??= allocateRaster(info);
        if (terrainRaster is null) return null;
        var updates = terrainImage is null ? null : settledUpdates;
        var canvas = terrainRaster.Canvas;
        using var restore = new SKAutoCanvasRestore(canvas, true);
        terrainImage?.Dispose();
        if (updates is null) canvas.DrawColor(Background, SKBlendMode.Src);
        canvas.Scale((float)frame.Scaling);
        var work = DrawGround(canvas, scene, frame, MapTileArt.Detail(frame.Camera.Zoom), null,
            reuseAtlas: reuseAtlas, settledUpdates: updates);
        terrainKey = frame;
        terrainImage = terrainRaster.Snapshot();
        settledUpdates ??= [];
        settledUpdates.Clear();
        long pixels = updates is null ? (long)info.Width * info.Height : work.PaintedPixels;
        return new(terrainImage, work, pixels, Scrolled: false, SeedBands: true);
    }

    private static bool CanPatchSettledTerrain(MapObservation map, FrameKey frame, SKImageInfo info)
    {
        int width = map.Width * MapCamera.HalfWidth + (map.Flat ? MapCamera.HalfWidth : 0);
        int height = (map.Height + 1) * MapCamera.HalfHeight;
        return CheckAxis(frame.Camera.X, frame.Width, info.Width, MapCamera.HalfWidth, width, !map.Flat) &&
            CheckAxis(frame.Camera.Y, frame.Height, info.Height, MapCamera.HalfHeight, height, wrap: false);

        bool CheckAxis(double origin, double logicalSize, int physicalSize, int margin, int extent, bool wrap)
        {
            double zoom = frame.Camera.Zoom;
            double first = -origin / zoom + margin, last = (logicalSize - origin) / zoom + margin;
            int firstCopy = wrap ? (int)Math.Floor(first / extent) : 0;
            int lastCopy = wrap ? (int)Math.Ceiling(last / extent) - 1 : 0;
            for (int copy = firstCopy; copy <= lastCopy; copy++)
            {
                double shift = (double)copy * extent;
                int head = Math.Max(0, (int)Math.Floor((first - shift) / ChunkSize) - 1);
                int tail = Math.Min((extent - 1) / ChunkSize, (int)Math.Ceiling((last - shift) / ChunkSize) - 1);
                for (int chunk = head; chunk <= tail; chunk++)
                {
                    int start = chunk * ChunkSize, span = Math.Min(ChunkSize, extent - start);
                    if (!wrap && start + span == extent) break;
                    double world = start + shift - margin;
                    float left = (float)(origin + world * zoom);
                    float edge = (float)(origin + (world + span) * zoom);
                    float right = left + (float)(span * zoom);
                    if (right <= edge) continue;

                    // The opaque nearest-sampled draws meet at this boundary.
                    // If float registration creates an overlap near a pixel
                    // boundary, keep the full ordered replay of both chunks.
                    double from = edge * (float)frame.Scaling, to = right * (float)frame.Scaling;
                    if (to <= 0 || from >= physicalSize) continue;
                    const double guard = 1d / 256;
                    if (Math.Floor(2 * (from - guard)) != Math.Floor(2 * (to + guard))) return false;
                }
            }
            return true;
        }
    }

    private TerrainFrame? PrepareTerrainFrame(MapScene scene, FrameKey frame, SKImageInfo info)
    {
        if (settledUpdates is not null) ClearTerrainFrame();
        if (!StableTerrainSampling(frame, info))
        {
            ClearTerrainRasters(keepScratch: (long)info.Width * info.Height >= LargeBandPixels);
            return null;
        }
        terrainRaster ??= allocateRaster(info);
        if (terrainRaster is null) return null;
        using var damage = ScrollDamage(scene, frame, info, out int dx, out int dy);
        bool scrolled = false;
        if (damage is not null)
        {
            viewportScratch ??= allocateRaster(info);
            if (viewportScratch is not null)
            {
                (terrainRaster, viewportScratch) = (viewportScratch, terrainRaster);
                using var copy = new SKPaint { BlendMode = SKBlendMode.Src };
                terrainRaster.Canvas.DrawImage(terrainImage, dx, dy, CacheSampling, copy);
                scrolled = true;
            }
        }
        ClearTerrainFrame();
        var canvas = terrainRaster.Canvas;
        using var restore = new SKAutoCanvasRestore(canvas, true);
        long painted = (long)info.Width * info.Height;
        if (scrolled)
        {
            canvas.ClipRegion(damage!);
            painted = 0;
            using var rectangles = damage!.CreateRectIterator();
            while (rectangles.Next(out var rect)) painted += (long)rect.Width * rect.Height;
        }
        canvas.DrawColor(Background, SKBlendMode.Src);
        canvas.Scale((float)frame.Scaling);
        var work = DrawGround(canvas, scene, frame, MapTileArt.Detail(frame.Camera.Zoom),
            scrolled ? ToLogical(damage!.Bounds, frame.Scaling) : null, scrolled ? damage : null);
        terrainKey = frame;
        terrainImage = terrainRaster.Snapshot();
        return new(terrainImage, work, painted, scrolled);
    }

    private SKRegion? ScrollDamage(MapScene scene, FrameKey next, SKImageInfo info, out int dx, out int dy)
    {
        dx = dy = 0;
        if (terrainImage is null || next.Camera.Zoom != terrainKey.Camera.Zoom ||
            terrainKey with { Camera = next.Camera } != next) return null;

        double x = next.Camera.X - terrainKey.Camera.X;
        if (!scene.Map.Flat)
        {
            double period = scene.Map.Width * MapCamera.HalfWidth * next.Camera.Zoom;
            x -= Math.Round(x / period) * period;
        }
        double physicalX = x * next.Scaling, physicalY = (next.Camera.Y - terrainKey.Camera.Y) * next.Scaling;
        dx = (int)Math.Round(physicalX);
        dy = (int)Math.Round(physicalY);
        if (Math.Abs(physicalX - dx) > 1e-7 || Math.Abs(physicalY - dy) > 1e-7 || (dx == 0 && dy == 0)) return null;

        const int gutter = 2;
        var overlap = new SKRectI(gutter + Math.Max(0, dx), gutter + Math.Max(0, dy),
            info.Width - gutter + Math.Min(0, dx), info.Height - gutter + Math.Min(0, dy));
        if (overlap.Width <= 0 || overlap.Height <= 0 || (long)overlap.Width * overlap.Height <= (long)info.Width * info.Height / 2)
            return null;

        var damage = new SKRegion(new SKRectI(0, 0, info.Width, info.Height));
        damage.Op(overlap, SKRegionOperation.Difference);
        return damage;
    }

    private static bool StableTerrainSampling(FrameKey frame, SKImageInfo info)
    {
        // Skia's nearest sampler can pick either adjacent texel when float
        // rounding reaches a sampling boundary. Use the full viewport
        // transform whenever an output pixel is close to such a boundary.
        double zoom = frame.Camera.Zoom * frame.Scaling;
        double sourcePerPixel = GroundRasterTier(zoom) / zoom;
        return StableAxis(info.Width, frame.Camera.X * frame.Scaling) &&
            StableAxis(info.Height, frame.Camera.Y * frame.Scaling);

        bool StableAxis(int length, double origin)
        {
            const double boundaryGuard = .02;
            for (int i = 0; i < length; i++)
            {
                double source = (i + .5 - origin) * sourcePerPixel;
                if (Math.Abs(source - Math.Round(source)) < boundaryGuard) return false;
            }
            return true;
        }
    }

    private void ClearTerrainFrame()
    {
        terrainImage?.Dispose();
        terrainImage = null;
        terrainKey = default;
        settledUpdates = null;
    }

    private void ClearTerrainRasters(bool keepScratch = false)
    {
        ClearTerrainFrame();
        terrainRaster?.Dispose();
        terrainRaster = null;
        if (!keepScratch)
        {
            viewportScratch?.Dispose();
            viewportScratch = null;
        }
    }

    private static void DrawRaster(SKCanvas canvas, SKImage image, double scaling) =>
        canvas.DrawImage(image, new SKRect(0, 0, (float)(image.Width / scaling), (float)(image.Height / scaling)), CacheSampling);

    private static SKRectI ToBacking(SKRect box, double scale) => new(
        (int)Math.Floor(box.Left * scale), (int)Math.Floor(box.Top * scale),
        (int)Math.Ceiling(box.Right * scale), (int)Math.Ceiling(box.Bottom * scale));

    private static SKRect ToLogical(SKRectI box, double scale) => new(
        (float)(box.Left / scale), (float)(box.Top / scale),
        (float)(box.Right / scale), (float)(box.Bottom / scale));
}
