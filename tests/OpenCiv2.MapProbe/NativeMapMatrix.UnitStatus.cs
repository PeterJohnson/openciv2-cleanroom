using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void UnitStatusPixelChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        const int width = 104, height = 80, shieldX = 62, shieldY = 16;
        var camera = new MapCamera(46, 44, 1);
        var board = MapGallery.Open("MV12").Map;
        var unit = new ObservedMapUnit(default, new(3), new(1), new(new(3), new(101)), MapUnitDetails.Withheld);
        var secondary = unit with { Type = new(2), Target = new(new(4), new(102)) };
        var map = board with
        {
            Width = 2,
            Height = 1,
            Tiles = new([new ObservedTile(1, default, false, 0, false)]),
            Cities = new([]),
            Annotations = MapAnnotations.Stock
        };
        using var painter = new MapPainter(atlas, fonts);
        var healthSamples = new List<object>();
        var orderSamples = new List<object>();
        var present = MapFact<MapPresence>.Known(MapPresence.Present);
        var healthCases = new (string Id, MapHealth? Value, int Max, long Remaining, string? Band)[]
        {
            ("full", new(30, 0), 30, 30, "healthGreen"),
            ("last-green", new(30, 9), 30, 21, "healthGreen"),
            ("first-yellow", new(30, 10), 30, 20, "healthYellow"),
            ("last-yellow", new(30, 19), 30, 11, "healthYellow"),
            ("first-red", new(30, 20), 30, 10, "healthRed"),
            ("zero", new(30, 30), 30, 0, "healthRed"),
            ("negative-remaining", new(30, 90), 30, -60, "healthRed"),
            ("negative-rule", new(-8, 5), -8, -13, "healthRed"),
            ("zero-rule", new(0, 2), 10, 8, "healthGreen"),
            ("simplified", new(30, 90, true), 30, 30, "healthGreen"),
            ("withheld", null, 0, 0, null),
            ("unavailable", null, 0, 0, null)
        };
        var orderCases = new (UnitOrder Order, string Label)[]
        {
            (UnitOrder.None, "–"), (UnitOrder.Fortifying, "f"), (UnitOrder.Fortified, "F"), (UnitOrder.Sleeping, "Z"),
            (UnitOrder.Fortress, "Ft"), (UnitOrder.Road, "Rd"), (UnitOrder.Irrigate, "Ir"), (UnitOrder.Mine, "Mi"),
            (UnitOrder.Transform, "Tr"), (UnitOrder.Clean, "Cl"), (UnitOrder.Airbase, "Ab"), (UnitOrder.GoTo, "G")
        };

        foreach (int density in new[] { 1, 2 })
        {
            int imageWidth = width * density, imageHeight = height * density;
            using var image = SKSurface.Create(new SKImageInfo(imageWidth, imageHeight));
            using var baseline = SKSurface.Create(new SKImageInfo(imageWidth, imageHeight));
            using var healthSheet = SKSurface.Create(new SKImageInfo(imageWidth * 6, imageHeight * 2));
            using var orderSheet = SKSurface.Create(new SKImageInfo(imageWidth * 7, imageHeight * 2));
            image.Canvas.Scale(density);
            baseline.Canvas.Scale(density);
            var bar = Backing(shieldX + 1, shieldY + 2, 9, 3);
            var barWithFringe = Backing(shieldX, shieldY + 1, 11, 5);
            Render(baseline, unit.Details);
            foreach (var (id, value, max, remaining, band) in healthCases)
            {
                var health = value is { } hp ? MapFact<MapHealth>.Known(hp) :
                    id == "withheld" ? MapFact<MapHealth>.Withheld : default;
                var details = unit.Details with { Health = health };
                string inspect = Render(image, details);
                Require(MapPainter.HealthBand(health) == band && inspect.Contains(
                    value is null ? $"Health {id}" : $"Health {remaining}/{max}", StringComparison.Ordinal),
                    $"health case {id} keeps its signed source value and observer state");
                Require(DifferencesOutside(image, baseline, imageWidth, imageHeight, barWithFringe) == 0,
                    $"health case {id} stays within the banner");
                int colored = 0;
                using (var pixels = image.PeekPixels())
                    for (int y = bar.Top; y < bar.Bottom; y++)
                        for (int x = bar.Left; x < bar.Right; x++)
                        {
                            var pixel = pixels.GetPixelColor(x, y);
                            if (band is not null && pixel == Opaque(MapArtCatalog.Stock.DisplayColors[band])) colored++;
                            if (band is null)
                                Require(!new[] { "healthRed", "healthYellow", "healthGreen" }
                                    .Any(name => pixel == Opaque(MapArtCatalog.Stock.DisplayColors[name])),
                                    "unavailable health does not draw a severity color");
                        }
                Require(band is null || colored >= density * density, $"health {id} is readable at {density}×");
                if (id is "full" or "simplified")
                    Require(colored == bar.Width * bar.Height, "healthy cases fill the whole bar");
                if (id is "zero" or "negative-remaining" or "negative-rule")
                    Require(colored == bar.Height * density, "invalid or exhausted values stay as a narrow severity marker");
                int index = healthSamples.Count % healthCases.Length;
                Record(healthSheet, index, 6);
                healthSamples.Add(new { id, density, value, max, remaining, band, colored, inspect, digest = Pixels(image) });
            }

            var orderBase = unit.Details with { Health = MapFact<MapHealth>.Known(new(30, 10)), Work = MapFact<int>.Known(231), Veteran = present };
            var orderBox = Backing(shieldX, shieldY + 6, 12, 9);
            Render(baseline, orderBase);
            var glyphs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (order, label) in orderCases)
                CheckOrder(orderBase with { Order = MapFact<UnitOrder>.Known(order) }, label, $"Order {order}",
                    altersPose: order is UnitOrder.Sleeping or UnitOrder.Fortified);
            CheckOrder(orderBase with { Automated = present, Order = MapFact<UnitOrder>.Known(UnitOrder.GoTo) }, "A", "Automated");
            CheckOrder(orderBase with { Waiting = present, Automated = present }, "?", "Waiting for input");
            Encode(healthSheet, Path.Combine(output, $"MV12-health-{density}x.png"));
            Encode(orderSheet, Path.Combine(output, $"MV13-orders-{density}x.png"));

            void CheckOrder(MapUnitDetails details, string label, string context, bool altersPose = false)
            {
                string inspect = Render(image, details);
                Require(MapPainter.OrderLabel(details) == label && inspect.Contains(context, StringComparison.Ordinal) &&
                    inspect.Contains("Veteran", StringComparison.Ordinal) && inspect.Contains("Accumulated work 231", StringComparison.Ordinal) &&
                    inspect.Contains("2 visible in stack", StringComparison.Ordinal),
                    $"admitted {context} is distinguished with health, work, veteran state and stack count");
                if (!altersPose)
                    Require(DifferencesOutside(image, baseline, imageWidth, imageHeight, orderBox) == 0, $"{context} stays inside its order badge");
                var tones = new List<uint>();
                using (var pixels = image.PeekPixels())
                    for (int py = orderBox.Top + density; py < orderBox.Bottom - density; py++)
                        for (int px = orderBox.Left + density; px < orderBox.Right - density; px++)
                        {
                            var pixel = pixels.GetPixelColor(px, py);
                            tones.Add(pixel.Red >= 120 && pixel.Green >= 110 && pixel.Blue >= 90 ? (uint)pixel : 0);
                        }
                Require(tones.Any(tone => tone != 0) && glyphs.Add(string.Join(",", tones)), $"{context} has distinct visible glyph pixels at {density}×");
                Record(orderSheet, glyphs.Count - 1, 7);
                orderSamples.Add(new { label, density, inspect, digest = Pixels(image) });
            }

            string Render(SKSurface surface, MapUnitDetails details)
            {
                var scene = new MapScene(map with { Units = new([unit with { Details = details }, secondary]) });
                painter.Draw(surface.Canvas, painter.SetScene(scene), camera, width, height, density, null);
                return MapInspection.Describe(scene, new(default, MapPickKind.Unit));
            }

            SKRectI Backing(int x, int y, int w, int h) => new(x * density, y * density, (x + w) * density, (y + h) * density);

            void Record(SKSurface target, int index, int columns)
            {
                using var screenshot = image.Snapshot();
                target.Canvas.DrawImage(screenshot, index % columns * imageWidth, index / columns * imageHeight,
                    new SKSamplingOptions(SKFilterMode.Nearest));
            }
        }
        File.WriteAllText(Path.Combine(output, "MV12-MV13-unit-status.json"), JsonSerializer.Serialize(new
        {
            width,
            height,
            camera,
            map.Identity,
            healthSamples,
            orderSamples,
            images = new[] { "MV12-health-1x.png", "MV12-health-2x.png", "MV13-orders-1x.png", "MV13-orders-2x.png" }
                .ToDictionary(path => path, path => FileHash(Path.Combine(output, path)))
        }, Json));
    }

    private static void StationaryAircraftPixelChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        const int width = 120, height = 90, bodyX = 20, bodyY = 16;
        var camera = new MapCamera(bodyX + 32, bodyY + 32, 1);
        var baseTile = new ObservedTile(1, MapImprovements.CitySite | MapImprovements.Fortification, false, 0, false)
        {
            AirbaseOccupant = MapFact<MapTileOwnership>.Known(new(new(4)))
        };
        var board = MapGallery.Open("MV14").Map with
        {
            Width = 2,
            Height = 1,
            Tiles = new([baseTile]),
            Cities = new([]),
            Annotations = MapAnnotations.Stock
        };
        var unit = new ObservedMapUnit(default, new(27), new(4), null, MapUnitDetails.Withheld);
        var stationed = unit with
        {
            Details = unit.Details with
            {
                StationaryAircraft = MapFact<MapPresence>.Known(MapPresence.Present)
            }
        };
        var sleeping = unit with { Details = unit.Details with { Order = MapFact<UnitOrder>.Known(UnitOrder.Sleeping) } };
        using var painter = new MapPainter(atlas, fonts);
        using var textures = new MapTextures(atlas);
        using var image = SKSurface.Create(new SKImageInfo(width, height));
        using var expected = SKSurface.Create(new SKImageInfo(width, height));
        using var samePose = SKSurface.Create(new SKImageInfo(width, height));
        using var glyph = new SKPaint { IsAntialias = true };
        var roles = MapArtCatalog.Stock;
        var samples = new List<object>();
        var noEffect = Render(image, unit, SentryAppearance.Silhouette);
        foreach (var mode in new[] { SentryAppearance.Silhouette, SentryAppearance.Greyscale })
        {
            var pixels = Render(image, stationed, mode);
            Render(expected, null, mode);
            var canvas = expected.Canvas;
            var unitShadow = Opaque(roles.DisplayColors["unitShadow"]);
            PaintObject(canvas, textures, "unit/27", bodyX + 2, bodyY + 2, unitShadow);
            if (mode == SentryAppearance.Silhouette)
                PaintObject(canvas, textures, "unit/27", bodyX, bodyY, Opaque(roles.DisplayColors["sentrySilhouette"]));
            else
            {
                // Three identical channels of luminance and one unchanged
                // alpha channel. The renderer's filter is not called here.
                using var grey = SKColorFilter.CreateColorMatrix(
                    [.2126f, .7152f, .0722f, 0, 0, .2126f, .7152f, .0722f, 0, 0, .2126f, .7152f, .0722f, 0, 0, 0, 0, 0, 1, 0]);
                using var ink = new SKPaint { ColorFilter = grey };
                canvas.DrawImage(textures.Get("unit/27"), SKRect.Create(bodyX, bodyY, 64, 48),
                    new SKSamplingOptions(SKFilterMode.Linear), ink);
            }
            int shieldX = bodyX + 48, shieldY = bodyY + 4;
            PaintObject(canvas, textures, "shield", shieldX + 1, shieldY + 1, unitShadow);
            PaintObject(canvas, textures, "shield", shieldX, shieldY, Opaque(roles.Factions[4].Rgb));
            glyph.Color = new SKColor(0xd91a2427);
            canvas.DrawRect(SKRect.Create(shieldX + 1, shieldY + 2, 9, 3), glyph);
            glyph.Color = new SKColor(0xff8a8f91);
            canvas.DrawLine(shieldX + 1, shieldY + 5, shieldX + 10, shieldY + 2, glyph);
            Require(Pixels(image) == Pixels(expected) && DifferentMapPixels(noEffect, pixels) > 0,
                $"stationary aircraft {mode} preserves the pinned body alpha, runway, ground and shield");
            Render(samePose, sleeping, mode);
            var order = new SKRectI(shieldX, shieldY + 6, shieldX + 12, shieldY + 15);
            Require(DifferencesOutside(image, samePose, width, height, order) == 0,
                $"{mode} treats the independently admitted sleep order and airbase state as the same body pose");
            string file = $"MV14-aircraft-{mode.ToString().ToLowerInvariant()}.png";
            Encode(image, Path.Combine(output, file));
            samples.Add(new { mode, file, sha256 = FileHash(Path.Combine(output, file)) });
        }
        File.WriteAllText(Path.Combine(output, "MV14-aircraft.json"), JsonSerializer.Serialize(new
        {
            width,
            height,
            camera,
            board.Identity,
            baseTile,
            unit = stationed,
            samples
        }, Json));

        uint[] Render(SKSurface target, ObservedMapUnit? model, SentryAppearance mode)
        {
            var scene = new MapScene(board with { Units = model is { } observed ? new([observed]) : new([]) });
            painter.Draw(target.Canvas, painter.SetScene(scene), camera, width, height, 1, null, new(Sentry: mode));
            return ReadMapPixels(target);
        }
    }
}
