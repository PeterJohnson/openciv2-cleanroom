using OpenCiv2.Contracts;
using SkiaSharp;

namespace OpenCiv2.MapRendering;

public sealed partial class MapPainter
{
    private static void ClipRegion(SKCanvas canvas, MapScene scene, FrameKey frame)
    {
        if (scene.Annotations.Region is null)
        {
            return;
        }

        var camera = frame.Camera;
        float dx = (float)(MapCamera.HalfWidth * camera.Zoom), dy = (float)(MapCamera.HalfHeight * camera.Zoom);
        using var outlines = new SKPathBuilder();
        foreach (var (point, display) in camera.Visible(scene.Map, frame.Width, frame.Height))
        {
            if (!scene.InRegion(point))
            {
                continue;
            }

            float x = (float)camera.ScreenX(display), y = (float)camera.ScreenY(point.Y);
            outlines.MoveTo(x, y - dy);
            outlines.LineTo(x + dx, y);
            outlines.LineTo(x, y + dy);
            outlines.LineTo(x - dx, y);
            outlines.Close();
        }
        using var path = outlines.Detach();
        canvas.ClipPath(path);
    }

    private void DrawTileOverlay(
        MapArtCanvas canvas,
        MapScene scene,
        MapPoint point,
        ObservedTile tile,
        float x,
        float y,
        float zoom,
        double tier,
        MapDisplay options)
    {
        if (tile.Terrain is null || (!options.Ownership && !options.Grid && !options.CityRadius))
        {
            return;
        }

        float left = x - 32 * zoom, top = y - 16 * zoom;
        if (options.Ownership && tile.Ownership.Value?.Owner is CivilizationSlot owner)
        {
            var color = Color(scene, owner);
            canvas.DrawImage(
                textures.OwnershipFill(tier, color.WithAlpha(95)),
                SKRect.Create(left, top, 64 * zoom, 32 * zoom),
                Sampling);
        }
        if (options.CityRadius && scene.InCityRadius(point))
        {
            Sprite(
                canvas,
                Catalog.Overlays["cityRadius"],
                left,
                top,
                zoom,
                tier,
                tint: Color(scene, scene.Map.Identity.Observer));
        }
        else if (options.Grid)
        {
            Sprite(canvas, Catalog.Overlays["grid"], left, top, zoom, tier);
        }
    }

    private static void StrokeDiamond(SKCanvas canvas, float x, float y, float zoom, SKColor color)
    {
        using var ink = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f
        };
        using var points = new SKPathBuilder();
        points.MoveTo(x, y - 16 * zoom);
        points.LineTo(x + 32 * zoom, y);
        points.LineTo(x, y + 16 * zoom);
        points.LineTo(x - 32 * zoom, y);
        points.Close();
        using var path = points.Detach();
        canvas.DrawPath(path, ink);
    }

    /// <summary>MV20 presentation of already-admitted segments. A break in allowed
    /// knowledge breaks the line, and a wrapped segment follows the nearest copy.</summary>
    private static void DrawPaths(MapArtCanvas canvas, MapScene scene, MapCamera camera, double width, double height)
    {
        if (scene.Annotations.Paths is not { } paths)
        {
            return;
        }

        using var pathPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            StrokeCap = SKStrokeCap.Round
        };
        using var dashes = SKPathEffect.CreateDash([5, 4], 0);
        foreach (var path in paths)
        {
            if (!scene.PermitsPath(path))
            {
                continue;
            }

            pathPaint.Color = path.Kind switch
            {
                MapPathKind.ActiveRoute => new(0xffead88a),
                MapPathKind.TradePartner => new(0xff7bcfda),
                MapPathKind.TradeBonusSatisfied => new(0xff98d666),
                _ => new(0xffed8d69)
            };
            pathPaint.PathEffect = path.Kind == MapPathKind.TradeBonusUnsatisfied ? dashes : null;
            for (int i = 1; i < path.Points.Count; i++)
            {
                var a = path.Points[i - 1];
                var b = path.Points[i];
                if (!scene.PermitsSegment(a, b, out int destinationX))
                {
                    continue;
                }

                double x1 = camera.ScreenX(a.X), x2 = camera.ScreenX(destinationX);
                double y1 = camera.ScreenY(a.Y), y2 = camera.ScreenY(b.Y);
                if (Math.Min(y1, y2) > height || Math.Max(y1, y2) < 0)
                {
                    continue;
                }

                double period = scene.Map.Width * MapCamera.HalfWidth * camera.Zoom;
                int start = scene.Map.Flat ? 0 : (int)Math.Ceiling((-Math.Max(x1, x2)) / period);
                int end = scene.Map.Flat ? 0 : (int)Math.Floor((width - Math.Min(x1, x2)) / period);
                for (int copy = start; copy <= end; copy++)
                {
                    canvas.DrawLine(
                        (float)(x1 + copy * period),
                        (float)y1,
                        (float)(x2 + copy * period),
                        (float)y2,
                        pathPaint);
                }
            }
        }
    }
}

public sealed partial class MapPainter
{
    private SKImage? overviewImage;

    private void ClearOverview()
    {
        overviewImage?.Dispose();
        overviewImage = null;
    }

    public void DrawOverview(SKCanvas canvas, long epoch, double width, double height,
        MapCamera camera, double mainWidth, double mainHeight, MapPoint? selected)
    {
        lock (gate)
        {
            var current = Volatile.Read(ref binding);
            if (!disposed && epoch == current.Epoch && current.Scene is { } sceneToShow)
            {
                ApplyScene(sceneToShow);
                if (sceneError is null && backendError is null)
                {
                    overviewImage ??= PrepareOverview(sceneToShow);
                }
            }
            using var restore = new SKAutoCanvasRestore(canvas, true);
            canvas.ClipRect(SKRect.Create((float)width, (float)height));
            canvas.DrawColor(new(0xff192a2b), SKBlendMode.Src);
            if (disposed || epoch != current.Epoch || !CanPresent(epoch) || overviewImage is null
                || scene is not { } active)
            {
                return;
            }

            var world = active.Map;
            var box = MapOverviewGeometry.Inner(world, width, height);
            canvas.DrawImage(overviewImage, box, CacheSampling);
            using var ink = new SKPaint { Color = HudText, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
            foreach (var footprint in MapOverviewGeometry.Footprint(world, box, camera, mainWidth, mainHeight))
            {
                canvas.DrawRect(footprint, ink);
            }

            if (selected is { } point && active.Contains(point) && active.InRegion(point))
            {
                ink.Style = SKPaintStyle.Fill;
                ink.Color = Color("selectionHighlight");
                canvas.DrawCircle(MapOverviewGeometry.CenterOf(world, box, point), 2.5f, ink);
            }
        }
    }

    private SKImage PrepareOverview(MapScene scene)
    {
        var map = scene.Map;
        using var bitmap = new SKBitmap(map.Width / 2, map.Height);
        var pixels = new SKColor[map.Tiles.Count];
        for (int i = 0; i < map.Tiles.Count; i++)
        {
            int row = i / bitmap.Width;
            var point = new MapPoint(2 * (i % bitmap.Width) + (row & 1), row);
            var tile = map.Tiles[i];
            pixels[i] = !scene.InRegion(point) || tile.Terrain is null ? new(0xff293d42) :
                tile.Terrain == 10 ? new(0xff315b75) : new(0xff728862);
            if (!scene.InRegion(point))
            {
                continue;
            }

            if (scene.Objects(point).City?.Details is { } city)
            {
                pixels[i] = Color(scene, city.Owner);
            }
            else if (MapScene.RememberedCity(tile))
            {
                pixels[i] = new(0xffb9a991);
            }
        }
        bitmap.Pixels = pixels;
        return SKImage.FromBitmap(bitmap);
    }
}

public sealed partial class MapPainter
{
    private int DrawTransient(SKCanvas canvas, MapScene scene, FrameKey frame, VisualFrame? visual, MapPick? inspected,
        MapPick? selected, SKImage? backdrop)
    {
        using (new SKAutoCanvasRestore(canvas, true))
        {
            ClipRegion(canvas, scene, frame);
            DrawWorldTransient(canvas, scene, frame, visual, inspected, selected, backdrop);
        }
        if (visual is null || visual.Notices.Count == 0)
        {
            return 0;
        }

        using var textPaint = new SKPaint { IsAntialias = true };
        using var textFont = text.Font(13);
        float left = 12, top = 12;
        int shown = 0;
        foreach (string line in visual.Notices)
        {
            textFont.Size = 13;
            float width = Math.Min((float)frame.Width - 24, text.Measure(line, textFont, textPaint).Width + 18);
            if (width <= 0 || top + 25 > frame.Height)
            {
                break;
            }

            Badge(canvas, textPaint, textFont, line, SKRect.Create(left, top, width, 25), 13, HudBackground);
            top += 29;
            shown++;
        }
        return shown;
    }

    private void DrawWorldTransient(
        SKCanvas canvas,
        MapScene scene,
        FrameKey frame,
        VisualFrame? visual,
        MapPick? inspected,
        MapPick? selected,
        SKImage? backdrop)
    {
        var camera = frame.Camera;
        float zoom = (float)camera.Zoom;
        DrawInspected(canvas, scene, frame, inspected);
        UnitTarget? focus = frame.Display.Bare || !frame.Display.ReducedMotion && visual?.Pulse == false
            ? null : scene.Annotations.ActiveUnit;
        if (focus is { } active && frame.Moving?.Contains(active) != true && scene.Locate(active) is { } position &&
            scene.InRegion(position))
        {
            if (backdrop is null || MapTileArt.Detail(zoom) != MapDetail.Overview ||
                !TryDrawOverviewFocus(canvas, backdrop, scene, frame, position, selected, inspected))
            {
                foreach (var center in SelectionCenters(scene.Map, frame, position))
                {
                    DrawActive(canvas, center.X, center.Y, zoom);
                }
            }
        }
        if (visual is null)
        {
            return;
        }

        if (!frame.Display.Bare && !frame.Display.ReducedMotion && visual.Effects.Count != 0)
        {
            using var ink = new SKPaint { IsAntialias = true };
            using var font = text.Font(11);
            using var grey = SKColorFilter.CreateColorMatrix(Greyscale);
            using var bodyPaint = new SKPaint();
            foreach (var row in visual.Effects)
            {
                var effect = row.Effect;
                double xp = effect.From.X + (row.WrappedDestinationX - effect.From.X) * row.Progress;
                double yp = effect.From.Y + (effect.To.Y - effect.From.Y) * row.Progress;
                if (effect.Kind is MapEffectKind.Explosion or MapEffectKind.Nuclear or MapEffectKind.CombatParticipant)
                {
                    xp = row.WrappedDestinationX;
                    yp = effect.To.Y;
                }
                double x = camera.X + xp * MapCamera.HalfWidth * zoom;
                float y = (float)(camera.Y + yp * MapCamera.HalfHeight * zoom);
                double period = scene.Map.Width * MapCamera.HalfWidth * zoom;
                double radius = 64 * zoom;
                int start = scene.Map.Flat ? 0 : (int)Math.Ceiling((-radius - x) / period);
                int end = scene.Map.Flat ? 0 : (int)Math.Floor((frame.Width + radius - x) / period);
                for (int copy = start; copy <= end; copy++)
                {
                    float center = (float)(x + copy * period);
                    double tier = zoom * frame.Scaling;
                    switch (effect.Kind)
                    {
                        case MapEffectKind.Move when effect.Unit is { } unit:
                            DrawUnit(canvas, scene, unit, 1, center - 32 * zoom, y - 32 * zoom, zoom, tier,
                                MapTileArt.Detail(camera.Zoom), frame.Display, ink, bodyPaint, font, grey);
                            if (focus is not null && unit.Target == focus)
                            {
                                DrawActive(canvas, center, y, zoom);
                            }

                            break;
                        case MapEffectKind.CombatParticipant:
                            StrokeDiamond(canvas, center, y, zoom, new(0xffff9055));
                            break;
                        case MapEffectKind.Explosion:
                        case MapEffectKind.Nuclear:
                            var frames = effect.Kind == MapEffectKind.Explosion ? Catalog.Explosion : Catalog.Nuclear;
                            string key = frames[MapVisualTimeline.FrameIndex(effect.Kind, row.Progress)];
                            var logical = textures.Logical(key);
                            Sprite(canvas, key, center - logical.X * zoom / 2, y - (logical.Y - 10) * zoom, zoom, tier);
                            break;
                    }
                }
            }
        }
    }

    private static void DrawInspected(SKCanvas canvas, MapScene scene, FrameKey frame, MapPick? inspected)
    {
        if (inspected is not { } cursor || !scene.InRegion(cursor.Position))
        {
            return;
        }

        float zoom = (float)frame.Camera.Zoom;
        foreach (var center in SelectionCenters(scene.Map, frame, cursor.Position))
        {
            StrokeDiamond(canvas, center.X, center.Y, zoom * 0.9f, new(0xff88c8cd));
        }
    }

    private bool TryDrawOverviewFocus(
        SKCanvas canvas,
        SKImage backdrop,
        MapScene scene,
        FrameKey frame,
        MapPoint position,
        MapPick? selected,
        MapPick? inspected)
    {
        float zoom = (float)frame.Camera.Zoom;
        var bounds = new SKRectI(0, 0, backdrop.Width, backdrop.Height);
        var patch = SKRectI.Empty;
        var centers = SelectionCenters(scene.Map, frame, position).ToArray();
        float reachX = 32 * zoom * .72f + 3, reachY = 16 * zoom * .72f + 3;
        foreach (var center in centers)
        {
            var box = ToBacking(
                new(center.X - reachX, center.Y - reachY, center.X + reachX, center.Y + reachY),
                frame.Scaling);
            if (box.IntersectsWith(bounds))
            {
                patch.Union(SKRectI.Intersect(bounds, box));
            }
        }
        if (patch.IsEmpty)
        {
            return true;
        }

        // At overview scale the GPU and CPU can cover different pixels for
        // the same thin stroke. Composite the outline over a small opaque
        // raster with the viewport pixel phase and the same overlay order.
        var padded = patch;
        padded.Inflate(DamageGuard, DamageGuard);
        padded = SKRectI.Intersect(padded, bounds);
        using var surface = allocateRaster(viewportInfo.WithSize(padded.Width, padded.Height));
        if (surface is null)
        {
            return false;
        }

        var raster = surface.Canvas;
        raster.Translate(-padded.Left, -padded.Top);
        raster.DrawImage(backdrop, 0, 0, CacheSampling);
        raster.Scale((float)frame.Scaling);
        DrawSelection(raster, scene, frame, selected);
        ClipRegion(raster, scene, frame);
        DrawInspected(raster, scene, frame, inspected);
        foreach (var center in centers)
        {
            DrawActive(raster, center.X, center.Y, zoom);
        }

        using var image = surface.Snapshot();
        var source = SKRect.Create(patch.Left - padded.Left, patch.Top - padded.Top, patch.Width, patch.Height);
        canvas.DrawImage(image, source, ToLogical(patch, frame.Scaling), CacheSampling);
        return true;
    }

    private static void DrawActive(SKCanvas canvas, float x, float y, float zoom) =>
        StrokeDiamond(canvas, x, y, zoom * 0.72f, new(0xffe17d57));
}
