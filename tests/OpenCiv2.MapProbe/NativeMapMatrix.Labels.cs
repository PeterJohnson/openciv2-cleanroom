using System.Text.Json;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Graphics;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void LabelAndFallbackChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        const int width = 378, height = 210;
        const string name = "Château de l’Île Nord et le port des navigateurs de l’Est";
        const string nearby = "Other harbor";
        const string notice = "É⤀⌒\U0001F6F8";
        const uint fill = 0xd91a2427, ink = 0xfffff0d1;
        var baseline = MapGallery.Open("MV27").Map;
        var first = baseline.Cities[0] with { Position = new(4, 4), Name = Classic(name) };
        var adjacent = baseline.Cities[1] with { Position = new(6, 4), Name = Classic(nearby) };
        var observed = baseline with
        {
            Width = 14,
            Height = 10,
            Tiles = new(Enumerable.Repeat(new ObservedTile(1, default, false, 0, false), 70)),
            Units = new([]),
            Cities = new([adjacent, first]),
            Annotations = MapAnnotations.Stock
        };
        using var sans = ReadFace("sans.regular");
        using var serif = ReadFace("serif.regular");
        using var mono = ReadFace("mono.regular");
        using var paint = new SKPaint { IsAntialias = true };
        using var primary = new SKFont(sans, 10) { Edging = SKFontEdging.Antialias };
        using var alternate = new SKFont(serif, 13) { Edging = SKFontEdging.Antialias };
        using var technical = new SKFont(mono, 13) { Edging = SKFontEdging.Antialias };
        using var painter = new MapPainter(atlas, fonts);
        using var layout = new MapText(fonts);
        using var fallbackFont = layout.Font(13);
        var measured = layout.Measure(notice, fallbackFont, paint);
        Require(!primary.ContainsGlyph('⤀') && alternate.ContainsGlyph('⤀') && !primary.ContainsGlyph('⌒') &&
            !alternate.ContainsGlyph('⌒') && technical.ContainsGlyph('⌒'), "pinned glyph coverage requires both fallbacks");
        Require(measured.Runs is { Count: 4 } runs && runs.Select(run => run.Face).SequenceEqual([0, 1, 2, 0]) &&
            runs.Select(run => run.Text).SequenceEqual(["É", "⤀", "⌒", "�"]), "fallback keeps face order and a deterministic missing-glyph marker");
        string clusters = "A\u0301é\U0001F6F8BC";
        var truncated = layout.Fit(clusters, layout.Measure("A\u0301…", fallbackFont, paint).Width + .1f, fallbackFont, paint);
        Require(truncated.Text == "A\u0301…", "ellipsizing preserves the complete accented text element");

        var records = new List<object>();
        foreach (int density in new[] { 1, 2 })
        {
            int pixelsWide = (int)(width * density), pixelsHigh = (int)(height * density);
            using var actual = SKSurface.Create(new SKImageInfo(pixelsWide, pixelsHigh));
            using var expected = SKSurface.Create(new SKImageInfo(pixelsWide, pixelsHigh));
            actual.Canvas.Scale((float)density);
            expected.Canvas.Scale((float)density);
            var outputs = new List<object>();
            foreach (double zoom in new[] { .4, .63 })
            {
                var camera = new MapCamera(170 - first.Position.X * 32 * zoom, 85 - first.Position.Y * 16 * zoom, zoom);
                var nameless = new MapScene(observed with { Cities = new(observed.Cities.Select(city => city with { Name = new([]) })) });
                painter.Draw(expected.Canvas, painter.SetScene(nameless), camera, width, height, density, null);
                uint[] background = ReadMapPixels(expected);
                var scene = new MapScene(observed);
                var draw = painter.Draw(actual.Canvas, painter.SetScene(scene), camera, width, height, density, null);
                Require(draw.Labels == 1 && MapInspection.Describe(scene, new(adjacent.Position, MapPickKind.City)).Contains(nearby, StringComparison.Ordinal),
                    "map row order resolves the crowded caption and the other admitted name stays inspectable");
                primary.Size = 10;
                string caption = Enumerable.Range(0, name.Length + 1).Select(i => i == name.Length ? name : name[..i] + "…")
                    .Last(prefix => primary.MeasureText(prefix, paint) <= 173);
                Require(caption.EndsWith('…') && MapInspection.Describe(scene, new(first.Position, MapPickKind.City)).Contains(name, StringComparison.Ordinal),
                    "the caption is shortened and inspection returns the full accented name");
                float advance = primary.MeasureText(caption, paint);
                float y = (float)camera.ScreenY(first.Position.Y) + 21 * (float)zoom;
                SKRect box = SKRect.Create(170 - (advance + 8) / 2, y - 10, advance + 8, 14);
                var canvas = expected.Canvas;
                Panel(canvas, box);
                paint.Color = new(ink);
                float baselineY = box.MidY - (primary.Metrics.Descent + primary.Metrics.Ascent) / 2;
                canvas.Save();
                canvas.ClipRect(box);
                canvas.DrawText(caption, box.MidX, baselineY, SKTextAlign.Center, primary, paint);
                canvas.Restore();
                paint.Color = new(0xff000000 | MapArtCatalog.Stock.Factions[first.Details!.Owner.Value].Rgb);
                canvas.DrawRect(box.Left, box.Top + 2, 2, box.Height - 4, paint);
                int altered = DifferentMapPixels(background, ReadMapPixels(actual));
                Require(altered > 0 && Pixels(expected) == Pixels(actual), $"caption uses the 10-DIP floor and pinned measure at {zoom}/{density}×");
                string captionPng = $"MV27-caption-{(zoom < .5 ? "reduced" : "full")}-{density}x.png";
                Encode(actual, Path.Combine(output, captionPng));
                outputs.Add(new { zoom, density, caption, bounds = new { box.Left, box.Top, box.Right, box.Bottom }, altered, captionPng });

                var hud = new VisualFrame(new([]), new([]), new([notice]), true);
                painter.Draw(expected.Canvas, painter.SetScene(scene), camera, width, height, density, null);
                var stats = painter.Draw(actual.Canvas, painter.SetScene(scene), camera, width, height, density, null, visual: hud);
                primary.Size = 13;
                (string Text, SKFont Font)[] known = [("É", primary), ("⤀", alternate), ("⌒", technical), ("�", primary)];
                float total = known.Sum(run => run.Font.MeasureText(run.Text, paint));
                var panel = SKRect.Create(12, 12, total + 18, 25);
                Panel(canvas, panel);
                canvas.Save();
                canvas.ClipRect(panel);
                paint.Color = new(ink);
                float x = panel.MidX - total / 2;
                float baselineYHud = panel.MidY - (primary.Metrics.Descent + primary.Metrics.Ascent) / 2;
                foreach (var (value, face) in known)
                {
                    canvas.DrawText(value, x, baselineYHud, SKTextAlign.Left, face, paint);
                    x += face.MeasureText(value, paint);
                }
                canvas.Restore();
                Require(stats.Notices == 1 && Pixels(expected) == Pixels(actual), "HUD fallback and shared metrics stay at viewport scale");
                if (zoom > .5) Encode(actual, Path.Combine(output, $"MV27-font-fallback-{density}x.png"));
            }

            var framed = new MapCamera(170 - 4 * 32 * .63, 85 - 4 * 16 * .63, .63);
            var region = new MapRegion(new([first.Position]));
            var clipped = observed with { Annotations = MapAnnotations.Stock with { Region = region } };
            painter.Draw(actual.Canvas, painter.SetScene(new(clipped)), framed, width, height, density, null);
            uint[] withCaption = ReadMapPixels(actual);
            var clippedBareName = clipped with { Cities = new([first with { Name = new([]) }]) };
            painter.Draw(expected.Canvas, painter.SetScene(new(clippedBareName)), framed, width, height, density, null);
            uint[] withoutCaption = ReadMapPixels(expected);
            int visible = 0;
            for (int row = 0; row < pixelsHigh; row++)
                for (int col = 0; col < pixelsWide; col++)
                {
                    int at = row * pixelsWide + col;
                    if (withCaption[at] == withoutCaption[at]) continue;
                    float distance = MathF.Abs((col + .5f) / density - 170) / (32 * .63f) +
                        MathF.Abs((row + .5f) / density - 85) / (16 * .63f);
                    Require(distance < 1.09f, "constrained annotation pixels stay inside the explicit region");
                    visible++;
                }
            Require(visible > 0, "selected region preserves the visible part of its caption");

            var drawScale = new MapCamera(75, 52, .625);
            var unit = new ObservedMapUnit(first.Position, new(1), new(1), null, MapUnitDetails.Withheld);
            var numberMap = observed with { Cities = new([]), Units = new([unit]) };
            float unitX = (float)drawScale.ScreenX(unit.Position.X);
            float unitY = (float)drawScale.ScreenY(unit.Position.Y);
            float shieldX = unitX + 16 * .625f, shieldY = unitY - 28 * .625f;
            int workPixels = CompareBadge(numberMap, numberMap with
            {
                Units = new([unit with { Details = unit.Details with { Work = MapFact<int>.Known(255) } }])
            }, "255", shieldX, Math.Max(shieldY + 15 * .625f, shieldY + 6 * .625f + 10), 9, 16, 7, new(fill), new(ink));
            var populous = first with
            {
                Name = new([]),
                Details = first.Details! with
                { Appearance = MapFact<MapCityAppearance>.Known(new(0, 0, false)), Population = default, Garrison = default, Disorder = default }
            };
            var town = observed with { Cities = new([populous]) };
            var ownColor = new SKColor(0xff000000 | MapArtCatalog.Stock.Factions[populous.Details!.Owner.Value].Rgb);
            int populationPixels = CompareBadge(town, town with
            {
                Cities = new([populous with { Details = populous.Details with { Population = MapFact<int>.Known(127) } }])
            }, "127", unitX - 25 * .625f, unitY - 15 * .625f, 10, 10, 8, ownColor, SKColors.White);
            records.Add(new { density, outputs, clippedCaptionPixels = visible, workPixels, populationPixels });

            int CompareBadge(MapObservation clear, MapObservation annotated, string digits,
                float left, float top, float h, float minimumWidth, float size, SKColor background, SKColor foreground)
            {
                painter.Draw(expected.Canvas, painter.SetScene(new(clear)), drawScale, width, height, density, null);
                var untouched = ReadMapPixels(expected);
                painter.Draw(actual.Canvas, painter.SetScene(new(annotated)), drawScale, width, height, density, null);
                primary.Size = size;
                var box = SKRect.Create(left, top, Math.Max(minimumWidth, MathF.Ceiling(primary.MeasureText(digits, paint) + 2)), h);
                var canvas = expected.Canvas;
                paint.Color = background;
                canvas.DrawRoundRect(box, 2, 2, paint);
                paint.Color = foreground;
                canvas.Save();
                canvas.ClipRect(box);
                canvas.DrawText(digits, box.MidX, box.MidY - (primary.Metrics.Descent + primary.Metrics.Ascent) / 2,
                    SKTextAlign.Center, primary, paint);
                canvas.Restore();
                int changed = DifferentMapPixels(untouched, ReadMapPixels(actual));
                int difference = DifferentMapPixels(ReadMapPixels(expected), ReadMapPixels(actual));
                if (difference > 0)
                {
                    Encode(actual, Path.Combine(output, $"MV27-badge-{digits}-{density}x.png"));
                    Encode(expected, Path.Combine(output, $"MV27-badge-{digits}-{density}x-reference.png"));
                }
                Require(changed > 0 && difference == 0, $"{digits} fits its badge at the declared {size}-DIP floor ({difference} differing pixels)");
                return changed;
            }
        }
        File.WriteAllText(Path.Combine(output, "MV27-labels.json"), JsonSerializer.Serialize(new { notice, measured, records }, Json));

        static OpenCiv2.Base.OwnedList<byte> Classic(string value) => new(ClassicText.EncodeWindows1252(value, "authored caption"));

        SKTypeface ReadFace(string id)
        {
            using var stream = fonts.OpenFace(id);
            return SKTypeface.FromStream(stream);
        }

        void Panel(SKCanvas canvas, SKRect box)
        {
            paint.Color = new(fill);
            canvas.DrawRoundRect(box, 2, 2, paint);
        }
    }
}
