using System.Diagnostics;
using SkiaSharp;

namespace OpenCiv2.MapRendering;

public sealed partial class MapPainter
{
    private const int BandEdge = 8;
    private const int LargeBandEdge = 16;
    private const int MinimumBandPixels = 2_000_000;
    private const int LargeBandPixels = 8_000_000;
    private const int MinimumForegroundObjects = 128;
    private static readonly ParallelOptions ForegroundWorkers = new()
    {
        MaxDegreeOfParallelism = Math.Min(6, Environment.ProcessorCount)
    };
    private static readonly ParallelOptions LargeForegroundWorkers = new()
    {
        MaxDegreeOfParallelism = Math.Min(12, Environment.ProcessorCount)
    };

    private static (ParallelOptions Workers, int Edge) BandsFor(SKRectI area) =>
        (long)area.Width * area.Height >= LargeBandPixels
            ? (LargeForegroundWorkers, LargeBandEdge)
            : (ForegroundWorkers, BandEdge);

    private bool CanPaintForegroundBands(MapScene scene, FrameKey frame, SKRectI area)
    {
        var (options, edge) = BandsFor(area);
        int workers = options.MaxDegreeOfParallelism;
        if (!parallelForeground || workers < 2 || OperatingSystem.IsBrowser() ||
            frame.Display.Bare || scene.Annotations.Region is not null ||
            area.Height / workers < edge * 4 || (long)area.Width * area.Height < MinimumBandPixels)
        {
            return false;
        }

        var display = frame.Display;
        return display.Grid || display.CityRadius || display.Ownership ||
            scene.Map.Units.Count + scene.Map.Cities.Count >= MinimumForegroundObjects;
    }

    private bool TryPaintForegroundBands(
        SKSurface raster,
        MapScene scene,
        FrameKey frame,
        List<CityLabel> labels,
        MapVisibleBounds bounds,
        MapDetail detail,
        SKPicture? groundPicture,
        SKRectI area,
        SKRect? damage,
        SKRegion? uncovered,
        SKImage? seed,
        out int objects,
        out int bands)
    {
        long started = Stopwatch.GetTimestamp();
        objects = bands = 0;
        var (scheduling, edge) = BandsFor(area);
        int workers = scheduling.MaxDegreeOfParallelism;
        // Worker canvases alias the owner's pixels but cannot notify its cached
        // snapshot/image generation. Discard alone does not invalidate the raster
        // pixel reference in this Skia build. Make an identity pixel write through
        // the owner before borrowing storage; this also performs any required COW.
        // SkiaSharp does not expose SkSurface.notifyContentWillChange.
        using (var before = raster.PeekPixels())
        {
            if (before is null)
            {
                return false;
            }

            using var restore = new SKAutoCanvasRestore(raster.Canvas, true);
            using var notify = new SKPaint
            {
                Color = before.GetPixelColor(area.Left, area.Top),
                BlendMode = SKBlendMode.Src
            };
            raster.Canvas.ResetMatrix();
            raster.Canvas.DrawRect(SKRect.Create(area.Left, area.Top, 1, 1), notify);
        }
        using var pixels = new SKPixmap();
        if (!raster.PeekPixels(pixels))
        {
            return false;
        }

        using var seedPixels = seed?.PeekPixels();
        if (seed is not null && seedPixels is null)
        {
            return false;
        }

        var info = pixels.Info;
        bool sharedScratch = damage is null && (seed is not null || (long)info.Width * info.Height >= LargeBandPixels);
        using var temporaryEdges = sharedScratch ? null : allocateRaster(info);
        var edges = sharedScratch ? viewportScratch ??= allocateRaster(info) : temporaryEdges;
        if (edges is null)
        {
            return false;
        }
        // The shared surface can have held a scrolled terrain raster. Each
        // aliased seam clip initializes its rows before replay.
        if (sharedScratch)
        {
            edges.Canvas.Discard();
        }

        using var edgePixels = new SKPixmap();
        if (!edges.PeekPixels(edgePixels))
        {
            return false;
        }

        var clips = new SKRectI[2 * workers - 1];
        for (int i = 0; i < workers; i++)
        {
            clips[i] = new(area.Left, Row(i), area.Right, Row(i + 1));
        }

        for (int i = 1; i < workers; i++)
        {
            int top = Row(i) - edge * 2;
            var clip = new SKRectI(area.Left, top, area.Right, top + edge * 4);
            if (groundPicture is null && seedPixels is null && !CopyRows(pixels, edgePixels, clip.Top, clip.Height))
            {
                return false;
            }

            clips[workers + i - 1] = clip;
        }
        var targets = new SKSurface?[clips.Length];
        try
        {
            for (int i = 0; i < targets.Length; i++)
            {
                // Each set of clips covers disjoint rows in its own backing.
                var storage = i < workers ? pixels : edgePixels;
                targets[i] = SKSurface.Create(info, storage.GetPixels(), storage.RowBytes);
                if (targets[i] is null)
                {
                    return false;
                }
            }

            // Resolve mutable textures and record once in normal depth order. Each
            // worker uses the same device origin and canvas transform. Integer row
            // clips give the canvases disjoint writes to the opaque backing raster.
            // Index recorded bounds so each replay visits the commands at its clip.
            long recordingAt = Stopwatch.GetTimestamp();
            using var recorder = new SKPictureRecorder();
            var recording = recorder.BeginRecording(PictureBounds(info, frame), useRTree: true);
            objects = PaintForeground(recording, scene, frame, labels, bounds, detail, damage);
            using var picture = recorder.EndRecording();
            long replayAt = Stopwatch.GetTimestamp();
            // Ground recording finished the background region. The workers
            // only read it and paint disjoint rows of each backing.
            int seedFailed = 0;
            Parallel.For(0, targets.Length, scheduling, i =>
            {
                var clip = clips[i];
                if (seedPixels is not null
                    && !CopyRows(seedPixels, i < workers ? pixels : edgePixels, clip.Top, clip.Height))
                {
                    Interlocked.Exchange(ref seedFailed, 1);
                    return;
                }
                Replay(targets[i]!.Canvas, clip);
            });
            if (seedFailed != 0)
            {
                return false;
            }
            // A hard raster clip can change antialiasing near its edge. Copy
            // the overlapping center rows from the separately padded replay.
            long repairAt = Stopwatch.GetTimestamp();
            for (int i = 1; i < workers; i++)
            {
                if (!CopyRows(edgePixels, pixels, Row(i) - edge, edge * 2))
                {
                    throw new MapResourceException("Could not compose foreground band edges");
                }
            }

            bands = workers;
            bandTicks = (
                recordingAt - started,
                replayAt - recordingAt,
                repairAt - replayAt,
                Stopwatch.GetTimestamp() - repairAt);
            return true;

            void Replay(SKCanvas canvas, SKRectI clip)
            {
                using var restore = new SKAutoCanvasRestore(canvas, true);
                canvas.ClipRect(new(clip.Left, clip.Top, clip.Right, clip.Bottom));
                if (groundPicture is not null)
                {
                    if (uncovered is null)
                    {
                        canvas.DrawColor(Background, SKBlendMode.Src);
                    }
                    else if (!uncovered.IsEmpty)
                    {
                        using var clear = new SKAutoCanvasRestore(canvas, true);
                        canvas.ClipRegion(uncovered);
                        canvas.DrawColor(Background, SKBlendMode.Src);
                    }
                }
                canvas.Scale((float)frame.Scaling);
                if (groundPicture is not null)
                {
                    canvas.DrawPicture(groundPicture);
                }

                canvas.DrawPicture(picture);
            }
        }
        finally
        {
            foreach (var target in targets)
            {
                target?.Dispose();
            }
        }

        int Row(int index) => area.Top + index * area.Height / workers;

        bool CopyRows(SKPixmap from, SKPixmap to, int top, int rows) =>
            from.ReadPixels(info.WithSize(area.Width, rows), to.GetPixels(area.Left, top), to.RowBytes, area.Left, top);
    }

    private static SKRect PictureBounds(SKImageInfo info, FrameKey frame)
    {
        var bounds = SKRect.Create((float)(info.Width / frame.Scaling), (float)(info.Height / frame.Scaling));
        // Some primitives just outside the viewport still reach its first or
        // last pixel through antialiasing. Keep them in the recorded index.
        bounds.Inflate(BandEdge, BandEdge);
        return bounds;
    }
}
