using System.Diagnostics;
using System.Globalization;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using SkiaSharp;

namespace OpenCiv2.App.Map;

internal sealed partial class MapPainter
{
    private static readonly MapArtCatalog Catalog = MapArtCatalog.Stock;
    private static readonly SKColor HudBackground = new(0xd91a2427);
    private static readonly SKColor HudText = new(0xfffff0d1);
    private static readonly SKColor Unavailable = new(0xff8a8f91);
    private static readonly float[] Greyscale =
    [
        .2126f,.7152f,.0722f,0,0, .2126f,.7152f,.0722f,0,0, .2126f,.7152f,.0722f,0,0, 0,0,0,1,0
    ];

    private MapDrawStats DrawBase(SKCanvas canvas, MapScene value, FrameKey frame, List<CityLabel> labels,
        SKRectI? damage = null, TerrainFrame? terrain = null, SKSurface? raster = null, bool replayGround = false,
        bool reuseGroundAtlas = false, MapDetail? detailOverride = null)
    {
        var camera = frame.Camera;
        var detail = detailOverride ?? MapTileArt.Detail(camera.Zoom);
        long start = Stopwatch.GetTimestamp();
        SKRect? logicalDamage = damage is { } patch ? ToLogical(patch, frame.Scaling) : null;
        var replayArea = damage ?? new SKRectI(0, 0, viewportInfo.Width, viewportInfo.Height);
        bool banded = raster is not null && CanPaintForegroundBands(value, frame, replayArea);
        // Replay the ground and foreground into the same rows during large
        // first frames or a full repaint of a retained viewport. Resolve all
        // chunk misses before parallel playback begins.
        using var groundRecorder = banded && replayGround && terrain is null ? new SKPictureRecorder() : null;
        if (terrain is null && groundRecorder is null) canvas.DrawColor(Background, SKBlendMode.Src);
        using var clip = new SKAutoCanvasRestore(canvas, true);
        ClipRegion(canvas, value, frame);
        var groundCanvas = groundRecorder?.BeginRecording(PictureBounds(viewportInfo, frame), useRTree: true) ?? canvas;
        bool gpuPreparation = textures.GpuPreparation;
        // The texture-only experiment offloads foreground preparation. CPU
        // ground workers continue to consume the existing prepared CPU images.
        if (!GpuGround) textures.GpuPreparation = false;
        using var uncovered = groundRecorder is not null && (long)replayArea.Width * replayArea.Height >= LargeBandPixels
            ? new SKRegion(replayArea) : null;
        var groundDraw = terrain?.Work ?? DrawGround(groundCanvas, value, frame, detail, logicalDamage,
            uncovered: uncovered, reuseAtlas: reuseGroundAtlas);
        textures.GpuPreparation = gpuPreparation;
        using var groundPicture = groundRecorder?.EndRecording();
        var seed = banded && terrain is { SeedBands: true } ? terrain.Image : null;
        if (terrain is not null && seed is null)
            DrawRaster(canvas, terrain.Image, frame.Scaling);
        long foregroundStart = Stopwatch.GetTimestamp();
        terrainTicks += foregroundStart - start;
        var bounds = camera.VisibleBounds(value.Map, frame.Width, frame.Height);
        if (paintingRegion)
            bounds = new(bounds.Left - 2, Math.Max(0, bounds.Top - 4), bounds.Right + 2, Math.Min(value.Map.Height - 1, bounds.Bottom + 4));
        int bands = 0;
        if (!banded || !TryPaintForegroundBands(raster!, value, frame, labels, bounds, detail, groundPicture,
                replayArea, logicalDamage, uncovered, seed, out int objects, out bands))
        {
            if (seed is not null) DrawRaster(canvas, seed, frame.Scaling);
            if (groundPicture is not null)
            {
                canvas.DrawColor(Background, SKBlendMode.Src);
                canvas.DrawPicture(groundPicture);
            }
            objects = PaintForeground(canvas, value, frame, labels, bounds, detail, logicalDamage);
        }
        foregroundTicks = Stopwatch.GetTimestamp() - foregroundStart;
        var groundPixels = damage ?? ToBacking(SKRect.Create((float)frame.Width, (float)frame.Height), frame.Scaling);
        return new(MapRedraw.Full, bounds.TileCount(), objects, groundDraw.Batches, groundDraw.Used, groundDraw.Prepared, labels.Count,
            GroundTier: groundDraw.Tier, GroundDamagePixels: terrain?.Pixels ?? (long)groundPixels.Width * groundPixels.Height,
            ForegroundBands: bands, GroundReplayBands: groundPicture is null ? 0 : bands);
    }

    private bool batchingForeground;
    private bool preparingAtlas;
    private readonly MapForegroundCommands foregroundCommands = new();
    private long foregroundPlanTicks, atlasPrepareTicks, foregroundSubmitTicks;
    private int foregroundTraversals, foregroundCommandCount;

    private int PaintForeground(SKCanvas canvas, MapScene value, FrameKey frame, List<CityLabel> labels,
        MapVisibleBounds bounds, MapDetail detail, SKRect? damage)
    {
        if (gpuComposition && gpuFeatures.HasFlag(MapGpuFeatures.Atlas | MapGpuFeatures.Commands))
        {
            foregroundCommands.Reset();
            textures.BeginCommandRead();
            try
            {
                long started = Stopwatch.GetTimestamp();
                int objects = PaintForegroundCore(new MapArtCanvas(foregroundCommands), value, frame, labels, bounds, detail, damage);
                long prepared = Stopwatch.GetTimestamp();
                foregroundPlanTicks += prepared - started;
                foregroundCommandCount += foregroundCommands.Count;
                foregroundCommands.Prepare(spriteAtlas);
                long submitted = Stopwatch.GetTimestamp();
                atlasPrepareTicks += submitted - prepared;
                foregroundCommands.Submit(canvas, spriteAtlas);
                foregroundSubmitTicks += Stopwatch.GetTimestamp() - submitted;
                return objects;
            }
            catch (MapCommandCapacityException)
            {
                foregroundCommands.Reset();
                textures.EndCommandRead();
                var requestedFeatures = gpuFeatures;
                gpuFeatures &= ~MapGpuFeatures.Commands;
                try { return PaintForeground(canvas, value, frame, labels, bounds, detail, damage); }
                finally { gpuFeatures = requestedFeatures; }
            }
            finally
            {
                foregroundCommands.Reset();
                textures.EndCommandRead();
                batchingForeground = false;
            }
        }
        if (gpuComposition && gpuFeatures.HasFlag(MapGpuFeatures.Atlas) && !preparingAtlas)
        {
            // Resolve all first-use sprites before submitting visible pixels.
            // Cold and warm frames then use the same atlas sampling path.
            using var preparation = SKSurface.CreateNull((int)Math.Ceiling(frame.Width), (int)Math.Ceiling(frame.Height));
            spriteAtlas.BeginPreparation();
            preparingAtlas = true;
            long start = Stopwatch.GetTimestamp();
            try { PaintForegroundCore(preparation.Canvas, value, frame, labels, bounds, detail, damage); }
            finally { preparingAtlas = false; }
            foregroundPlanTicks += Stopwatch.GetTimestamp() - start;
            start = Stopwatch.GetTimestamp();
            spriteAtlas.FinishPreparation();
            atlasPrepareTicks += Stopwatch.GetTimestamp() - start;
        }
        long submit = Stopwatch.GetTimestamp();
        int count = PaintForegroundCore(canvas, value, frame, labels, bounds, detail, damage);
        foregroundSubmitTicks += Stopwatch.GetTimestamp() - submit;
        return count;
    }

    private int PaintForegroundCore(MapArtCanvas canvas, MapScene value, FrameKey frame, List<CityLabel> labels,
        MapVisibleBounds bounds, MapDetail detail, SKRect? damage)
    {
        foregroundTraversals++;
        batchingForeground = gpuComposition && gpuFeatures.HasFlag(MapGpuFeatures.Atlas);
        using var paint = new SKPaint { IsAntialias = true };
        using var font = text.Font(11);
        using var greyscale = SKColorFilter.CreateColorMatrix(Greyscale);
        using var bodyPaint = new SKPaint();
        var camera = frame.Camera;
        float zoom = (float)camera.Zoom;
        double tier = zoom * frame.Scaling;
        if (frame.Display.Grid || frame.Display.CityRadius || frame.Display.Ownership)
            foreach (var (point, display) in camera.Visible(value.Map, frame.Width, frame.Height))
            {
                if (!value.InRegion(point)) continue;
                float x = (float)camera.ScreenX(display), y = (float)camera.ScreenY(point.Y);
                if (damage is { } patch && !patch.IntersectsWith(SKRect.Create(x - 32 * zoom, y - 16 * zoom, 64 * zoom, 32 * zoom)))
                    continue;
                DrawTileOverlay(canvas, value, point, value.Tile(point), x, y, zoom, tier, frame.Display);
            }

        int objects = 0;
        if (!frame.Display.Bare)
        {
            var remaining = frame.Moving is { } moving ? value.StacksWithout(moving) : null;
            preparedBodyTextures = overlappedBodyTextures = textures.CompleteBodyPreparation(admit: true);
            // A previous view at this scale may have shown different bodies.
            // Prepare the actual misses among currently admitted bindings.
            if (damage is null && preparedBodyTextures == 0 && textures.CanPrepare)
                preparedBodyTextures = textures.Prepare(VisibleBodyBindings(value, bounds, remaining), tier);
            foreach (var (point, display) in value.VisibleForeground(bounds))
            {
                var tile = value.Tile(point);
                var cell = ForegroundCell(value, point, remaining);
                var unit = cell.Unit;
                bool remembered = MapScene.RememberedCity(tile);
                if (cell.City is not null || remembered) objects++;
                if (unit is not null) objects += cell.Count;
                float x = (float)camera.ScreenX(display), y = (float)camera.ScreenY(point.Y);
                // Include custom marker anchors, shadows, badges, and antialiasing.
                if (damage is { } clipArea && !clipArea.IntersectsWith(BodyBounds(x, y, zoom))) continue;

                float left = x - 32 * zoom, top = y - 32 * zoom;
                DrawTallImprovements(canvas, value, tile, left, top, zoom, tier, detail);
                if (cell.City is { } city)
                {
                    DrawCity(canvas, value, city, left, top, zoom, tier, detail, paint, font);
                }
                else if (remembered)
                {
                    canvas.Flush(spriteAtlas);
                    paint.Color = new(0xffc2b9a3);
                    canvas.DrawRoundRect(x - 4, y - 4, 8, 8, 2, 2, paint);
                }

                if (unit is { } visibleUnit)
                {
                    // Cell depth is in map-row order; stack/banner/body are layered together,
                    // so a later sprite cannot be promoted across an overlapping translucent one.
                    float unitLeft = left + (cell.City is null ? 0 : 14 * zoom);
                    DrawUnit(canvas, value, visibleUnit, cell.Count, unitLeft, top, zoom, tier, detail, frame.Display, paint, bodyPaint, font, greyscale);
                }
                canvas.Flush(spriteAtlas);
            }
            DrawPaths(canvas, value, camera, frame.Width, frame.Height);
        }
        canvas.Flush(spriteAtlas);
        batchingForeground = false;
        PaintCityLabels(canvas, labels, frame, paint, font, damage);
        return objects;
    }

    private bool BeginColdBodyPreparation(MapScene scene, FrameKey frame)
    {
        double tier = (float)frame.Camera.Zoom * frame.Scaling;
        if (ground.Count != 0 || frame.Display.Bare || Environment.ProcessorCount < 8 || !textures.CanPrepare) return false;
        var bounds = frame.Camera.VisibleBounds(scene.Map, frame.Width, frame.Height);
        var remaining = frame.Moving is { } moving ? scene.StacksWithout(moving) : null;
        return textures.BeginBodyPreparation(VisibleBodyBindings(scene, bounds, remaining), tier);
    }

    private static MapObjects ForegroundCell(MapScene scene, MapPoint point, Dictionary<MapPoint, MapObjects>? remaining) =>
        remaining is not null && remaining.TryGetValue(point, out var withoutMoving) ? withoutMoving : scene.Objects(point);

    private static IEnumerable<string> VisibleBodyBindings(MapScene scene, MapVisibleBounds bounds, Dictionary<MapPoint, MapObjects>? remaining)
    {
        foreach (var (point, _) in scene.VisibleForeground(bounds))
        {
            var cell = ForegroundCell(scene, point, remaining);
            if (cell.City is { } city && CityBinding(city) is { } art) yield return art.Body;
            if (cell.Unit is { } unit) yield return UnitBinding(unit).Body;
        }
    }

    private static SKColor Rgb(uint value) => new(0xff000000 | value);
    private SKColor Color(MapScene scene, CivilizationSlot slot) => Rgb(textures.Color(scene.Annotations.ColorRole(slot)));
    private static SKColor Color(string role) => Rgb(Catalog.DisplayColors[role]);

    private void Sprite(MapArtCanvas canvas, string key, float x, float y, float zoom, double tier, SKPaint? paint = null, SKColor? tint = null)
    {
        if (!textures.HasStock && !key.StartsWith("unit/", StringComparison.Ordinal) && !key.StartsWith("city/", StringComparison.Ordinal))
            return;
        var logical = textures.Logical(key);
        bool mask = batchingForeground && tint is not null && gpuFeatures.HasFlag(MapGpuFeatures.Masks);
        // DrawAtlas's DstIn takes instance color as destination and image alpha
        // as source. Ordinary multicolor art can share its resident alpha without
        // allocating a white intermediate or a color-specific prepared image.
        bool whiteMask = mask && gpuFeatures.HasFlag(MapGpuFeatures.MaskBatch);
        var image = textures.Get(key, tier, mask ? whiteMask ? SKColors.White : null : tint);
        var destination = SKRect.Create(x, y, logical.X * zoom, logical.Y * zoom);
        if (batchingForeground)
        {
            var atlasKey = new MapGpuSpriteAtlas.Key(key, image.Width, image.Height,
                whiteMask ? uint.MaxValue : !mask && tint is SKColor color ? (uint)color : null);
            if (canvas.Commands is { } commands)
                commands.Add(new(MapForegroundCommands.Kind.Sprite, destination, Image: image, Key: atlasKey,
                    Sampling: Sampling, Paint: commands.Capture(paint), Tint: mask ? tint : null));
            else if (preparingAtlas)
            {
                if (MapGpuSpriteAtlas.CanBatch(image, destination, paint)) spriteAtlas.Collect(atlasKey, image);
            }
            else spriteAtlas.Draw(canvas.Immediate!, atlasKey, image, destination, Sampling, paint, mask ? tint : null);
        }
        else canvas.DrawImage(image, destination, Sampling, paint);
    }

    private void DrawTallImprovements(MapArtCanvas canvas, MapScene scene, ObservedTile tile, float x, float y,
        float zoom, double tier, MapDetail detail)
    {
        if (detail != MapDetail.Full || tile.Terrain is null || (tile.Improvements & MapImprovements.Fortification) == 0)
            return;
        string key = MapTileArt.Airbase(tile)
            ? tile.AirbaseOccupant.Value?.Owner is CivilizationSlot aircraft
                ? Catalog.Factions[scene.Annotations.ColorRole(aircraft)].OccupiedAirbase : Catalog.Improvements["airbase"]
            : Catalog.Improvements["fortress"];
        Sprite(canvas, key, x, y, zoom, tier);
    }

    private void DrawCity(MapArtCanvas canvas, MapScene scene, ObservedMapCity city, float x, float y, float zoom,
        double tier, MapDetail detail, SKPaint paint, SKFont font)
    {
        canvas.Flush(spriteAtlas);
        var info = city.Details;
        var art = CityBinding(city);
        if (art is null)
        {
            paint.Color = info is null ? Unavailable : Color(scene, info.Owner);
            canvas.DrawRoundRect(x + 28 * zoom, y + 24 * zoom, 8 * zoom, 8 * zoom, 2, 2, paint);
        }
        else Sprite(canvas, art.Body, x, y, zoom, tier);
        if (info is null) return;
        var faction = Catalog.Factions[scene.Annotations.ColorRole(info.Owner)];
        SKColor owner = Color(scene, info.Owner);
        if (info.Garrison.Value is { } garrison)
        {
            string flag = garrison == MapPresence.Present ? faction.BrightFlag : faction.DarkFlag;
            var anchor = art is null ? Catalog.FlagAnchor : textures.Flag(art.Body, Catalog.FlagAnchor);
            Sprite(canvas, flag, x + anchor.X * zoom, y + anchor.Y * zoom, zoom, tier);
        }
        if (detail == MapDetail.Full && info.Population.Value is int population)
        {
            canvas.Flush(spriteAtlas);
            float size = Math.Max(8, 10 * zoom);
            float height = Math.Max(12 * zoom, 10);
            string number = population.ToString(CultureInfo.InvariantCulture);
            float width = BadgeWidth(font, paint, number, size, height);
            // Keep admitted annotations visible when the city's body selector
            // is withheld. Put the badge left of the marker and above the caption.
            var anchor = art is null ? default : textures.Marker(art.Body, art.Population);
            var badge = art is null
                ? SKRect.Create(x + 14 * zoom - width, y + 28 * zoom - height, width, height)
                : SKRect.Create(x + anchor.X * zoom, y + anchor.Y * zoom, width, height);
            Badge(canvas, paint, font, number, badge, size, owner, PopulationInk(owner));
        }
        if (detail == MapDetail.Full && info.Disorder.Value == MapPresence.Present)
            Sprite(canvas, Catalog.Disorder, x + 16 * zoom, y + 6 * zoom, zoom, tier, tint: owner);
    }

    private static CityArt? CityBinding(ObservedMapCity city) =>
        city.Details?.Appearance.Value is { } appearance ? Catalog.Cities[appearance.StockIndex] : null;

    internal static UnitArt UnitBinding(ObservedMapUnit unit) => unit.Owner.Value == 0 && unit.Type.Value == 46
        ? Catalog.Leader : unit.Type.Value < Catalog.Units.Count ? Catalog.Units[unit.Type.Value]
        : throw new InvalidDataException($"Unsupported unit-art identity {unit.Type.Value}");

    private void DrawUnit(MapArtCanvas canvas, MapScene scene, ObservedMapUnit unit, int count, float x, float y, float zoom,
        double tier, MapDetail detail, MapDisplay display, SKPaint paint, SKPaint bodyPaint, SKFont font, SKColorFilter greyscale)
    {
        canvas.Flush(spriteAtlas);
        var art = UnitBinding(unit);
        var shieldColors = textures.ShieldColors(scene.Annotations.ColorRole(unit.Owner));
        var state = unit.Details;
        var rearShadow = Color("stackShadow");
        bool sleeping = state.Order.Value == UnitOrder.Sleeping || state.StationaryAircraft.Value == MapPresence.Present;
        if (detail != MapDetail.Overview)
        {
            if (state.Order.Value == UnitOrder.Fortified)
                Sprite(canvas, Catalog.Improvements["fortified"], x, y, zoom, tier);
            Sprite(canvas, art.Body, x + 2 * zoom, y + 2 * zoom, zoom, tier, tint: count > 1 ? rearShadow : Color("unitShadow"));
        }
        if (sleeping && display.Sentry == SentryAppearance.Silhouette)
            Sprite(canvas, art.Body, x, y, zoom, tier, tint: Color("sentrySilhouette"));
        else
        {
            bodyPaint.ColorFilter = sleeping ? greyscale : null;
            Sprite(canvas, art.Body, x, y, zoom, tier, bodyPaint);
        }

        var anchor = textures.Marker(art.Body, art.Shield);
        float shieldX = x + anchor.X * zoom, shieldY = y + anchor.Y * zoom;
        if (detail != MapDetail.Overview)
        {
            float outward = anchor.X < 32 ? -zoom : zoom;
            if (count > 1)
            {
                float secondaryX = shieldX + 4 * outward;
                Sprite(canvas, Catalog.Shield, secondaryX + outward, shieldY + zoom, zoom, tier, tint: rearShadow);
                Sprite(canvas, Catalog.Shield, secondaryX, shieldY, zoom, tier, tint: Rgb(shieldColors.Secondary));
            }
            Sprite(canvas, Catalog.Shield, shieldX + outward, shieldY + zoom, zoom, tier, tint: Color("unitShadow"));
        }
        Sprite(canvas, Catalog.Shield, shieldX, shieldY, zoom, tier, tint: Rgb(shieldColors.Primary));
        canvas.Flush(spriteAtlas);
        if (!textures.HasStock)
        {
            paint.Color = Rgb(shieldColors.Primary);
            canvas.DrawCircle(shieldX, shieldY + 10 * zoom, 2, paint);
        }

        float barWidth = Math.Max(9 * zoom, 3), barHeight = Math.Max(3 * zoom, 1);
        var bar = SKRect.Create(shieldX + zoom, shieldY + 2 * zoom, barWidth, barHeight);
        PaintHealth(canvas, paint, bar, state.Health);
        if (detail == MapDetail.Full)
        {
            string order = OrderLabel(state);
            float height = Math.Max(8 * zoom, 9);
            float size = Math.Max(7, 9 * zoom);
            float orderTop = shieldY + barHeight + 3 * zoom;
            float workTop = Math.Max(shieldY + 15 * zoom, orderTop + height + 1);
            string work = state.Work.Value?.ToString(CultureInfo.InvariantCulture) ?? "";
            float width = Math.Max(12 * zoom, 11);
            float workWidth = BadgeWidth(font, paint, work, size, Math.Max(18 * zoom, 16));
            if (order.Length != 0)
            {
                font.Size = size;
                float orderSize = Math.Clamp(size * (width - 2) / font.MeasureText(order, paint), 7, size);
                Badge(canvas, paint, font, order, SKRect.Create(shieldX, orderTop, width, height), orderSize, HudBackground);
            }
            if (count > 1)
            {
                string total = count.ToString(CultureInfo.InvariantCulture);
                float countWidth = BadgeWidth(font, paint, total, size, width);
                Badge(canvas, paint, font, total, SKRect.Create(shieldX + width, shieldY, countWidth, height), size, HudBackground);
            }
            if (work.Length != 0)
                Badge(canvas, paint, font, work, SKRect.Create(shieldX, workTop, workWidth, height), size, HudBackground);
            if (state.Veteran.Value == MapPresence.Present)
            {
                float radius = Math.Max(1, 1.3f * zoom);
                float veteranX = state.Work.Value is int ? shieldX + workWidth + radius + 2 : shieldX + radius + 2;
                paint.Color = HudText;
                canvas.DrawCircle(veteranX, workTop + height / 2, radius, paint);
            }
        }
    }

    internal static string OrderLabel(MapUnitDetails details)
    {
        if (details.Waiting.Value == MapPresence.Present) return "?";
        if (details.Automated.Value == MapPresence.Present) return "A";
        return details.Order.Value switch
        {
            UnitOrder.None => "–",
            UnitOrder.Fortifying => "f",
            UnitOrder.Fortified => "F",
            UnitOrder.Sleeping => "Z",
            UnitOrder.Fortress => "Ft",
            UnitOrder.Road => "Rd",
            UnitOrder.Irrigate => "Ir",
            UnitOrder.Mine => "Mi",
            UnitOrder.Transform => "Tr",
            UnitOrder.Clean => "Cl",
            UnitOrder.Airbase => "Ab",
            UnitOrder.GoTo => "G",
            _ => ""
        };
    }

    internal static string? HealthBand(MapFact<MapHealth> fact)
    {
        if (fact.Value is not { } hp) return null;
        if (hp.Maximum <= 0 || hp.Remaining * 3 <= hp.Maximum) return "healthRed";
        return hp.Remaining * 3 <= 2L * hp.Maximum ? "healthYellow" : "healthGreen";
    }

    private static void PaintHealth(MapArtCanvas canvas, SKPaint paint, SKRect bar, MapFact<MapHealth> health)
    {
        paint.Color = HudBackground;
        canvas.DrawRect(bar, paint);
        if (health.Value is not { } hp)
        {
            paint.Color = Unavailable;
            canvas.DrawLine(bar.Left, bar.Bottom, bar.Right, bar.Top, paint);
            return;
        }
        paint.Color = Color(HealthBand(health)!);
        float fraction = hp.Maximum > 0 ? Math.Clamp((float)hp.Remaining / hp.Maximum, 0f, 1f) : 0;
        canvas.DrawRect(SKRect.Create(bar.Left, bar.Top, Math.Max(bar.Width * fraction, 1), bar.Height), paint);
    }

    internal static SKColor PopulationInk(SKColor fill)
    {
        double luminance = .2126 * Linear(fill.Red) + .7152 * Linear(fill.Green) + .0722 * Linear(fill.Blue);
        return luminance >= .179 ? SKColors.Black : SKColors.White;

        static double Linear(byte channel)
        {
            double srgb = channel / 255d;
            return srgb <= .04045 ? srgb / 12.92 : Math.Pow((srgb + .055) / 1.055, 2.4);
        }
    }

    private static float BadgeWidth(SKFont font, SKPaint paint, string value, float size, float minimum)
    {
        if (value.Length == 0) return minimum;
        font.Size = size;
        return Math.Max(minimum, MathF.Ceiling(font.MeasureText(value, paint) + 2));
    }

    private void Badge(MapArtCanvas canvas, SKPaint paint, SKFont font, string value, SKRect box, float size, SKColor fill,
        SKColor? foreground = null)
    {
        paint.Color = fill;
        canvas.DrawRoundRect(box, 2, 2, paint);
        font.Size = size;
        var line = text.Fit(value, box.Width - 2, font, paint);
        canvas.Save();
        canvas.ClipRect(box);
        paint.Color = foreground ?? HudText;
        var metrics = font.Metrics;
        text.DrawCentered(canvas, line, box.MidX, box.MidY - (metrics.Descent + metrics.Ascent) / 2, font, paint);
        canvas.Restore();
    }

    private sealed record CityLabel(string Text, SKRect Box, SKColor Owner);

    private List<CityLabel> PlanLabels(MapScene scene, FrameKey frame)
    {
        var labels = new List<CityLabel>();
        if (frame.Display.Bare || MapTileArt.Detail(frame.Camera.Zoom) == MapDetail.Overview || scene.Map.Cities.Count == 0)
            return labels;
        using var paint = new SKPaint { IsAntialias = true };
        using var font = text.Font(11);
        var occupied = new HashSet<(int X, int Y)>();
        var camera = frame.Camera;
        float zoom = (float)camera.Zoom, size = LabelFontSize(frame);
        font.Size = size;
        float maximum = Math.Min(185, (float)frame.Width);
        var bounds = camera.VisibleBounds(scene.Map, frame.Width, frame.Height);
        foreach (var (point, display) in scene.VisibleLabels(bounds))
        {
            var cell = scene.Objects(point);
            if (cell.City is not { } city || cell.Name is not { Length: > 0 } name) continue;
            float x = (float)camera.ScreenX(display), y = (float)camera.ScreenY(point.Y) + 21 * zoom;
            var caption = text.Fit(name, maximum - 12, font, paint);
            if (caption.Text.Length == 0) continue;
            float width = caption.Width + 8;
            var box = SKRect.Create(x - width / 2, y - size, width, size + 4);
            if (box.Left < 0 || box.Top < 0 || box.Right > frame.Width || box.Bottom > frame.Height) continue;
            int left = (int)(box.Left / 20), right = (int)(box.Right / 20), top = (int)(box.Top / 10), bottom = (int)(box.Bottom / 10);
            bool free = true;
            for (int row = top; row <= bottom && free; row++)
                for (int column = left; column <= right && free; column++)
                    free = !occupied.Contains((column, row));
            if (!free) continue;
            for (int row = top; row <= bottom; row++)
                for (int column = left; column <= right; column++)
                    occupied.Add((column, row));
            labels.Add(new(caption.Text, box, city.Details is { } info ? Color(scene, info.Owner) : Unavailable));
        }
        return labels;
    }

    private static float LabelFontSize(FrameKey frame) => Math.Clamp((float)frame.Camera.Zoom * 11, 10, 22);

    private void PaintCityLabels(MapArtCanvas canvas, List<CityLabel> labels, FrameKey frame, SKPaint paint, SKFont font, SKRect? damage)
    {
        float size = LabelFontSize(frame);
        foreach (var (text, box, owner) in labels)
        {
            if (damage is { } clip && !clip.IntersectsWith(box)) continue;
            Badge(canvas, paint, font, text, box, size, HudBackground);
            paint.Color = owner;
            canvas.DrawRect(box.Left, box.Top + 2, 2, box.Height - 4, paint);
        }
    }
}
