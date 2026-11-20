using OpenCiv2.Contracts;
using SkiaSharp;

namespace OpenCiv2.MapRendering;

public sealed partial class MapPainter
{
    private const int RegionSide = 768, RegionGutter = 2;
    private const long RegionBudget = 128L * 1024 * 1024;
    private readonly record struct RegionKey(int Column, int Row, double Zoom, double Scaling, MapDetail Detail, MapDisplay Display,
        double PhaseX, double PhaseY);
    private sealed record RegionImage(RegionKey Key, FrameKey Frame, SKImage Image)
    {
        public long Bytes => 4L * Image.Width * Image.Height;
    }
    private readonly Dictionary<RegionKey, LinkedListNode<RegionImage>> gpuRegions = [];
    private readonly LinkedList<RegionImage> regionRecency = [];
    private MapScene? regionScene;
    private long regionBytes;
    private int regionHits, regionBuilds, regionEvictions;
    private bool paintingRegion;

    private void ClearGpuRegions()
    {
        foreach (var region in regionRecency) region.Image.Dispose();
        gpuRegions.Clear();
        regionRecency.Clear();
        regionBytes = 0;
        regionScene = null;
    }

    private void DropRegion(LinkedListNode<RegionImage> entry)
    {
        regionBytes -= entry.Value.Bytes;
        entry.Value.Image.Dispose();
        gpuRegions.Remove(entry.Value.Key);
        regionRecency.Remove(entry);
        regionEvictions++;
    }

    private MapDrawStats DrawGpuRegions(SKCanvas output, MapScene scene, FrameKey displayed, List<CityLabel> labels)
    {
        // Moving objects alter depth composition. Use the intact direct path for
        // that frame instead of compositing movers over stale stationary bodies.
        if (displayed.Moving is not null) return DrawBase(output, scene, displayed, labels) with { Redraw = MapRedraw.Direct };
        if (regionScene is { } before && !ReferenceEquals(before, scene))
        {
            var changes = scene.Map.Delta;
            if (!SameLayout(before, scene) || changes is null || !changes.Matches(before.Map, scene.Map) || changes.AnnotationsChanged)
                ClearGpuRegions();
            else
                for (var entry = regionRecency.First; entry is not null;)
                {
                    var next = entry.Next;
                    var region = entry.Value;
                    var damage = MeasureDamageRegions(before, scene, region.Frame, [], [], region.Image.Width, region.Image.Height);
                    if (damage is null || damage.Count != 0) DropRegion(entry);
                    entry = next;
                }
        }
        regionScene = scene;
        double backingZoom = gpuFeatures.HasFlag(MapGpuFeatures.TierCache)
            ? GroundRasterTier(displayed.Camera.Zoom * displayed.Scaling) / displayed.Scaling : displayed.Camera.Zoom;
        double ratio = displayed.Camera.Zoom / backingZoom;
        double scale = displayed.Scaling;
        bool phaseAligned = gpuFeatures.HasFlag(MapGpuFeatures.PanPhase) && ratio == 1;
        double phaseX = phaseAligned ? displayed.Camera.X * scale - Math.Floor(displayed.Camera.X * scale) : 0;
        double phaseY = phaseAligned ? displayed.Camera.Y * scale - Math.Floor(displayed.Camera.Y * scale) : 0;
        double left = -displayed.Camera.X * scale / ratio, top = -displayed.Camera.Y * scale / ratio;
        double right = left + displayed.Width * scale / ratio, bottom = top + displayed.Height * scale / ratio;
        int firstX = (int)Math.Floor(left / RegionSide), lastX = (int)Math.Ceiling(right / RegionSide) - 1;
        int firstY = (int)Math.Floor(top / RegionSide), lastY = (int)Math.Ceiling(bottom / RegionSide) - 1;
        var detail = MapTileArt.Detail(displayed.Camera.Zoom);
        long bytesPerRegion = 4L * (RegionSide + 2 * RegionGutter) * (RegionSide + 2 * RegionGutter);
        long visibleCount = (long)(lastX - firstX + 1) * (lastY - firstY + 1);
        if (visibleCount * bytesPerRegion > RegionBudget)
            return DrawBase(output, scene, displayed, labels) with { Redraw = MapRedraw.Direct };
        var required = new HashSet<RegionKey>();
        for (int y = firstY; y <= lastY; y++)
            for (int x = firstX; x <= lastX; x++)
                required.Add(new(x, y, backingZoom, scale, detail, displayed.Display, phaseX, phaseY));
        output.DrawColor(Background, SKBlendMode.Src);
        int built = 0, chunks = 0, prepared = 0, batches = 0;
        foreach (var key in required)
        {
            if (!gpuRegions.TryGetValue(key, out var resident))
            {
                while (regionBytes + bytesPerRegion > RegionBudget)
                {
                    var victim = regionRecency.First;
                    while (victim is not null && required.Contains(victim.Value.Key)) victim = victim.Next;
                    if (victim is null) throw new MapGpuResourceException("Visible map regions exceed their declared budget");
                    DropRegion(victim);
                }
                int side = RegionSide + 2 * RegionGutter;
                var camera = new MapCamera((phaseX + RegionGutter - key.Column * RegionSide) / scale,
                    (phaseY + RegionGutter - key.Row * RegionSide) / scale, backingZoom);
                var frame = new FrameKey(camera, side / scale, side / scale, scale, displayed.Display, null);
                using var recorder = new SKPictureRecorder();
                var canvas = recorder.BeginRecording(SKRect.Create(side, side));
                canvas.Scale((float)scale);
                MapDrawStats work;
                paintingRegion = true;
                try { work = DrawBase(canvas, scene, frame, [], detailOverride: detail); }
                finally { paintingRegion = false; }
                using var picture = recorder.EndRecording();
                var image = SKImage.FromPicture(picture, new(side, side)) ?? throw new MapGpuResourceException("Could not allocate map region");
                resident = regionRecency.AddLast(new RegionImage(key, frame, image));
                gpuRegions.Add(key, resident);
                regionBytes += resident.Value.Bytes;
                regionBuilds++;
                built++;
                chunks += work.GroundChunks; prepared += work.PreparedChunks; batches += work.Batches;
            }
            else
            {
                regionHits++;
                regionRecency.Remove(resident);
                regionRecency.AddLast(resident);
            }
            var source = SKRect.Create(RegionGutter, RegionGutter, RegionSide, RegionSide);
            var destination = SKRect.Create((float)(displayed.Camera.X + (key.Column * RegionSide - phaseX) * ratio / scale),
                (float)(displayed.Camera.Y + (key.Row * RegionSide - phaseY) * ratio / scale),
                (float)(RegionSide * ratio / scale), (float)(RegionSide * ratio / scale));
            output.DrawImage(resident.Value.Image, source, destination, phaseAligned ? CacheSampling : Sampling);
        }
        using var paint = new SKPaint { IsAntialias = true };
        using var font = text.Font(11);
        PaintCityLabels(output, labels, displayed, paint, font, null);
        var bounds = displayed.Camera.VisibleBounds(scene.Map, displayed.Width, displayed.Height);
        int objects = 0;
        if (!displayed.Display.Bare)
            foreach (var (point, _) in scene.VisibleForeground(bounds))
            {
                var cell = scene.Objects(point);
                if (cell.City is not null || MapScene.RememberedCity(scene.Tile(point))) objects++;
                if (cell.Unit is not null) objects += cell.Count;
            }
        return new(built == 0 ? MapRedraw.Cached : MapRedraw.Scrolled, bounds.TileCount(), objects, batches,
            chunks, prepared, labels.Count, GroundTier: GroundRasterTier(backingZoom * scale),
            DamagePixels: built * bytesPerRegion / 4, GroundDamagePixels: built * bytesPerRegion / 4);
    }
}
