using System.Text;
using System.Text.Json;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void OcclusionPixelChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        const int width = 350, height = 212, columns = 6, rows = 9;
        var catalog = MapArtCatalog.Stock;
        string effectKey = catalog.Explosion[2];
        var images = new Dictionary<string, byte[]>();
        var definitions = new Dictionary<string, object>();
        foreach (var (key, color) in new (string, SKColor)[]
        {
            ("unit/32", new(0xff21cc84)), ("unit/1", new(0xff207fed)), ("city/47", new(0xffd6a315)),
            ("improvement/fortress", new(0xff904ac7)), (effectKey, new(0xffff4a41))
        })
        {
            var logical = catalog.Images[key].Logical;
            using var bitmap = new SKBitmap(new SKImageInfo(logical.X, logical.Y, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            for (int y = 0; y < logical.Y; y++)
                for (int x = 0; x < logical.X; x++)
                {
                    bool inside = x >= 5 && y >= 5 && x < logical.X - 5 && y < logical.Y - 4;
                    byte alpha = !inside || x is >= 13 and <= 19 && y is >= 15 and <= 28
                        ? (byte)0 : x == 5 || y == 5 ? (byte)60 : y % 7 == 0 ? (byte)255 : (byte)154;
                    bitmap.SetPixel(x, y, color.WithAlpha(alpha));
                }
            using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            string path = $"{images.Count}.png";
            byte[] bytes = png.ToArray();
            images.Add(path, bytes);
            definitions.Add(key, new { path, sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)) });
        }
        images.Add("map-art.json", JsonSerializer.SerializeToUtf8Bytes(new { format = "openciv2-map-art/1", images = definitions }));
        var art = MapArtSelection.Load(native: path => new MemoryStream(images[path], writable: false));
        var camera = new MapCamera(23, 32, 1);
        var rear = new MapPoint(4, 4);
        var front = new MapPoint(5, 5);
        var board = MapGallery.Open("MV28").Map;
        var ground = Enumerable.Repeat(new ObservedTile(1, default, false, 0, false), columns * rows).ToArray();
        ground[front.Y * columns + front.X / 2] = ground[front.Y * columns + front.X / 2] with
        { Ownership = MapFact<MapTileOwnership>.Known(new(new(3))) };
        var terrainOnly = board with
        {
            Width = columns * 2,
            Height = rows,
            Flat = true,
            Tiles = new(ground),
            Units = new([]),
            Cities = new([]),
            Annotations = MapAnnotations.Stock
        };
        var tiles = ground.ToArray();
        tiles[rear.Y * columns + rear.X / 2] = tiles[rear.Y * columns + rear.X / 2] with { Improvements = MapImprovements.Fortification };
        var city = board.Cities[0] with
        {
            Position = rear,
            Name = new(Encoding.ASCII.GetBytes("Low Harbor")),
            Details = board.Cities[0].Details! with
            { Appearance = MapFact<MapCityAppearance>.Known(new(5, 3, true)), Garrison = default, Population = default, Disorder = default }
        };
        var lower = new ObservedMapUnit(front, new(1), new(3), null, MapUnitDetails.Withheld);
        var upper = new ObservedMapUnit(rear, new(32), new(1), null, MapUnitDetails.Withheld);
        var scene = new MapScene(terrainOnly with { Tiles = new(tiles), Cities = new([city]), Units = new([lower, upper]) });
        var preview = new MapEffect(1, MapEffectKind.Explosion, front, front, 300);
        var overlay = new VisualFrame(new([new EffectFrame(preview, .27, front.X)]), new([]), new([]), true);
        var display = new MapDisplay(Ownership: true, Grid: true);
        using var textFaceStream = fonts.OpenFace("sans.regular");
        using var textFace = SKTypeface.FromStream(textFaceStream);
        using var font = new SKFont(textFace, 11) { Edging = SKFontEdging.Antialias };
        using var textures = new MapTextures(atlas, art);
        using var painter = new MapPainter(atlas, fonts, art);
        var receipts = new List<object>();

        foreach (int density in new[] { 1, 2 })
        {
            using var actual = SKSurface.Create(new SKImageInfo(width * density, height * density));
            using var expected = SKSurface.Create(new SKImageInfo(width * density, height * density));
            using var backdrop = SKSurface.Create(new SKImageInfo(width * density, height * density));
            using var paint = new SKPaint { IsAntialias = true };
            actual.Canvas.Scale(density);
            expected.Canvas.Scale(density);
            // Use the actual frame's ground only, under the independently ordered art.
            backdrop.Canvas.Scale(density);
            painter.Draw(backdrop.Canvas, painter.SetScene(new(terrainOnly)), camera, width, height, density, null, display);
            using var groundImage = backdrop.Snapshot();
            long epoch = painter.SetScene(scene);
            var draw = painter.Draw(actual.Canvas, epoch, camera, width, height, density, null, display, overlay);
            string file = $"MV28-overlap-{density}x.png";
            Encode(actual, Path.Combine(output, file));
            var pixels = ReadMapPixels(actual);
            Reference();
            int differences = DifferentMapPixels(pixels, ReadMapPixels(expected));
            if (differences > 0) Encode(expected, Path.Combine(output, $"MV28-overlap-reference-{density}x.png"));
            Require(draw.Effects == 1 && draw.Labels == 1 && differences == 0,
                $"MV28 ordered alpha, overlay and caption match the independent composition at {density}× ({differences} pixels)");
            var contributions = new Dictionary<string, int>();
            foreach (string omitted in new[] { "fortress", "city", "rearUnit", "frontUnit", "caption", "effect" })
            {
                Reference(omitted);
                int changed = DifferentMapPixels(pixels, ReadMapPixels(expected));
                Require(changed > 0, $"MV28 {omitted} has visible pixels after all later layers");
                contributions.Add(omitted, changed);
            }
            Reference(swapDepth: true);
            int swapped = DifferentMapPixels(pixels, ReadMapPixels(expected));
            Require(swapped > 0, "reordering overlapping tall images by texture changes the result");
            var cached = painter.Draw(actual.Canvas, epoch, camera, width, height, density, null, display, overlay);
            Require(cached.Redraw == MapRedraw.Cached && DifferentMapPixels(pixels, ReadMapPixels(actual)) == 0,
                "retained world composites the translucent effect exactly once");
            receipts.Add(new { density, camera, file, sha256 = FileHash(Path.Combine(output, file)), contributions, swapped });

            void Reference(string? omit = null, bool swapDepth = false)
            {
                var canvas = expected.Canvas;
                canvas.DrawImage(groundImage, SKRect.Create(width, height), new SKSamplingOptions(SKFilterMode.Nearest));
                if (swapDepth && omit != "frontUnit") Unit(lower, 0, 48, 4);
                int x = (int)camera.ScreenX(rear.X), y = (int)camera.ScreenY(rear.Y);
                if (omit != "fortress") Sprite("improvement/fortress", x - 32, y - 32);
                if (omit != "city") Sprite("city/47", x - 32, y - 32);
                if (omit != "rearUnit") Unit(upper, 14, 2, 0);
                if (!swapDepth && omit != "frontUnit") Unit(lower, 0, 48, 4);
                if (omit != "caption")
                {
                    const string label = "Low Harbor";
                    float advance = font.MeasureText(label, paint);
                    var badge = SKRect.Create(x - (advance + 8) / 2, y + 10, advance + 8, 15);
                    paint.Color = new(0xd91a2427);
                    canvas.DrawRoundRect(badge, 2, 2, paint);
                    paint.Color = new(0xfffff0d1);
                    canvas.Save();
                    canvas.ClipRect(badge);
                    canvas.DrawText(label, x, badge.MidY - (font.Metrics.Descent + font.Metrics.Ascent) / 2, SKTextAlign.Center, font, paint);
                    canvas.Restore();
                    paint.Color = new(0xff000000 | catalog.Factions[city.Details!.Owner.Value].Rgb);
                    canvas.DrawRect(badge.Left, badge.Top + 2, 2, badge.Height - 4, paint);
                }
                if (omit != "effect") Sprite(effectKey, (int)camera.ScreenX(front.X) - 16, (int)camera.ScreenY(front.Y) - 22);
            }

            void Unit(ObservedMapUnit unit, int shift, int anchorX, int anchorY)
            {
                var canvas = expected.Canvas;
                int x = (int)camera.ScreenX(unit.Position.X) - 32 + shift, y = (int)camera.ScreenY(unit.Position.Y) - 32;
                string key = $"unit/{unit.Type.Value}";
                var shadow = new SKColor(0xff000000 | catalog.DisplayColors["unitShadow"]);
                Sprite(key, x + 2, y + 2, shadow);
                Sprite(key, x, y);
                x += anchorX;
                y += anchorY;
                Sprite(catalog.Shield, x + (anchorX < 32 ? -1 : 1), y + 1, shadow);
                Sprite(catalog.Shield, x, y, new(0xff000000 | catalog.Factions[unit.Owner.Value].Rgb));
                paint.Color = new(0xd91a2427);
                canvas.DrawRect(SKRect.Create(x + 1, y + 2, 9, 3), paint);
                paint.Color = new(0xff8a8f91);
                canvas.DrawLine(x + 1, y + 5, x + 10, y + 2, paint);
            }

            void Sprite(string key, int x, int y, SKColor? tint = null)
            {
                var logical = textures.Logical(key);
                expected.Canvas.DrawImage(textures.Get(key, density, tint), SKRect.Create(x, y, logical.X, logical.Y),
                    new SKSamplingOptions(SKFilterMode.Linear));
            }
        }
        File.WriteAllText(Path.Combine(output, "MV28-occlusion.json"), JsonSerializer.Serialize(receipts, Json));
    }
}
