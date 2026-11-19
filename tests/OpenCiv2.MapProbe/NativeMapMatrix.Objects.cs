using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void UnitArtPixelChecks(AssetAtlas atlas, PinnedFonts fonts, MapArtSelection? replacement, string theme, string output)
    {
        const int width = 100, height = 76, bodyX = 14, bodyY = 12;
        var camera = new MapCamera(bodyX + 32, bodyY + 32, 1);
        var board = MapGallery.Open("MV10").Map;
        var map = board with
        {
            Width = 2,
            Height = 1,
            Tiles = new([new ObservedTile(1, default, false, 0, false)]),
            Cities = new([]),
            Annotations = MapAnnotations.Stock
        };
        var catalog = MapArtCatalog.Stock;
        using var painter = new MapPainter(atlas, fonts, replacement);
        using var textures = new MapTextures(atlas, replacement);
        using var actual = SKSurface.Create(new SKImageInfo(width, height));
        using var expected = SKSurface.Create(new SKImageInfo(width, height));
        using var bodies = SKSurface.Create(new SKImageInfo(width * 8, height * 8));
        using var stacks = SKSurface.Create(new SKImageInfo(width * 8, height * 2));
        using var paint = new SKPaint { IsAntialias = true };
        var receipts = new List<object>();
        var roleReceipts = new List<object>();

        for (int i = 0; i < 64; i++)
        {
            bool leader = i == 63;
            var unit = new ObservedMapUnit(default, new((byte)(leader ? 46 : i)), new((byte)(leader ? 0 : 1)), null,
                MapUnitDetails.Withheld);
            string key = $"unit/{(leader ? 62 : i)}";
            var registered = Marker(i);
            Render(unit, [unit], MapAnnotations.Stock);
            Reference(key, registered, unit.Owner.Value, stacked: false);
            Require(Pixels(actual) == Pixels(expected), $"{theme} unit {i} draws its pinned body, shield and exact registration");
            Record(bodies, i);
            receipts.Add(new { i, unit.Type, unit.Owner, key, registered, digest = Pixels(actual) });
        }

        for (byte role = 0; role < 8; role++)
        {
            foreach (bool leftAnchor in new[] { false, true })
            {
                byte slot = leftAnchor ? (byte)32 : (byte)3;
                var unit = new ObservedMapUnit(default, new(slot), new(1), new(new(1), new(101)), MapUnitDetails.Withheld);
                var back = unit with { Target = new(new(2), new(102)), Type = new(5) };
                byte[] roles = MapAnnotations.Stock.FactionRoles.ToArray();
                roles[1] = role;
                var annotations = MapAnnotations.Stock with { FactionRoles = new(roles) };
                Render(unit, [unit, back], annotations);
                var registered = Marker(slot);
                Reference($"unit/{slot}", registered, role, stacked: true);
                int shieldX = bodyX + registered.X, shieldY = bodyY + registered.Y;
                var countBadge = new SKRectI(shieldX + 12, shieldY, shieldX + 24, shieldY + 9);
                Require(DifferencesOutside(actual, expected, width, height, countBadge) == 0 &&
                    DifferentMapPixels(ReadMapPixels(actual), ReadMapPixels(expected)) > 0,
                    $"{theme} stacked role {role} matches both masks and shadows, with a separately visible count");
                int cell = role + (leftAnchor ? 8 : 0);
                Record(stacks, cell);
                var colors = ExpectedShields(role);
                roleReceipts.Add(new { role, slot, registered, colors.Primary, colors.Secondary, digest = Pixels(actual) });
            }
        }

        string unitImage = $"MV10-{theme}-unit-art.png", stackImage = $"MV11-{theme}-stack-art.png";
        Encode(bodies, Path.Combine(output, unitImage));
        Encode(stacks, Path.Combine(output, stackImage));
        File.WriteAllText(Path.Combine(output, $"MV10-MV11-{theme}.json"), JsonSerializer.Serialize(new
        {
            width,
            height,
            density = 1,
            camera,
            map.Identity,
            theme,
            receipts,
            stacks = roleReceipts,
            units = new { file = unitImage, sha256 = FileHash(Path.Combine(output, unitImage)) },
            shields = new { file = stackImage, sha256 = FileHash(Path.Combine(output, stackImage)) }
        }, Json));

        void Render(ObservedMapUnit unit, ObservedMapUnit[] members, MapAnnotations annotations)
        {
            var scene = new MapScene(map with { Units = new(members), Annotations = annotations });
            var cell = scene.Objects(default);
            Require(cell.Unit == unit && cell.Count == members.Length, "unit display stack keeps the representative and exact count");
            painter.Draw(actual.Canvas, painter.SetScene(scene), camera, width, height, 1, null);
        }

        void Reference(string key, ArtPoint marker, byte role, bool stacked)
        {
            var canvas = expected.Canvas;
            canvas.Clear(new SKColor(0xff172b33));
            PaintGroundTile(canvas, textures, "terrain/1", bodyX, bodyY + 16);
            var stackShadow = Opaque(catalog.DisplayColors["stackShadow"]);
            var unitShadow = Opaque(catalog.DisplayColors["unitShadow"]);
            PaintObject(canvas, textures, key, bodyX + 2, bodyY + 2, stacked ? stackShadow : unitShadow);
            PaintObject(canvas, textures, key, bodyX, bodyY);
            int x = bodyX + marker.X, y = bodyY + marker.Y;
            int step = marker.X < 32 ? -1 : 1;
            var colors = ExpectedShields(role);
            if (stacked)
            {
                PaintObject(canvas, textures, "shield", x + 5 * step, y + 1, stackShadow);
                PaintObject(canvas, textures, "shield", x + 4 * step, y, Opaque(colors.Secondary));
            }
            PaintObject(canvas, textures, "shield", x + step, y + 1, unitShadow);
            PaintObject(canvas, textures, "shield", x, y, Opaque(colors.Primary));
            paint.Color = new SKColor(0xd91a2427);
            canvas.DrawRect(SKRect.Create(x + 1, y + 2, 9, 3), paint);
            paint.Color = new SKColor(0xff8a8f91);
            canvas.DrawLine(x + 1, y + 5, x + 10, y + 2, paint);
        }

        (uint Primary, uint Secondary) ExpectedShields(byte role)
        {
            if (theme != "classic")
            {
                uint stock = catalog.Factions[role].Rgb;
                return (stock, stock);
            }
            // These are the two distinct palette entries authored into the
            // synthetic CITIES sheet. This reference does not query its loader.
            return (AuthoredRgb(71 + role), AuthoredRgb(91 + role));
        }

        ArtPoint Marker(int i) => theme switch
        {
            "classic" => new(17, 6),
            "native" when i == 32 => new(22, 6),
            _ => i is >= 32 and <= 43 ? new(2, 0) : new(48, 4)
        };

        static uint AuthoredRgb(int index) => (uint)(((11 * 29 + index * 63) % 240 + 15) << 16 |
            ((11 * 83 + index * 23) % 240 + 15) << 8 | ((11 * 11 + index * 48) % 240 + 15));

        void Record(SKSurface sheet, int i)
        {
            using var snapshot = actual.Snapshot();
            sheet.Canvas.DrawImage(snapshot, (i % 8) * width, (i / 8) * height, new SKSamplingOptions(SKFilterMode.Nearest));
        }
    }

    private static void CityArtPixelChecks(AssetAtlas atlas, PinnedFonts fonts, MapArtSelection? replacement, string theme, string output)
    {
        const int width = 100, height = 80, bodyX = 14, bodyY = 12;
        var camera = new MapCamera(bodyX + 32, bodyY + 32, 1);
        var board = MapGallery.Open("MV15").Map;
        var map = board with
        {
            Width = 2,
            Height = 1,
            Tiles = new([new ObservedTile(1, MapImprovements.CitySite, false, 0, false)]),
            Units = new([]),
            Annotations = MapAnnotations.Stock
        };
        var catalog = MapArtCatalog.Stock;
        using var painter = new MapPainter(atlas, fonts, replacement);
        using var textures = new MapTextures(atlas, replacement);
        using var actual = SKSurface.Create(new SKImageInfo(width, height));
        using var expected = SKSurface.Create(new SKImageInfo(width, height));
        using var sheet = SKSurface.Create(new SKImageInfo(width * 8, height * 6));
        var receipts = new List<object>();

        for (int i = 0; i < 48; i++)
        {
            var original = board.Cities[i];
            var details = original.Details! with
            {
                Population = MapFact<int>.Withheld,
                Garrison = MapFact<MapPresence>.Withheld,
                Disorder = MapFact<MapPresence>.Withheld
            };
            var city = original with { Position = default, Name = new([]), Details = details };
            string key = $"city/{i}";
            Render(city);
            var canvas = expected.Canvas;
            canvas.Clear(new SKColor(0xff172b33));
            PaintGroundTile(canvas, textures, "terrain/1", bodyX, bodyY + 16);
            PaintObject(canvas, textures, key, bodyX, bodyY);
            Require(Pixels(actual) == Pixels(expected), $"{theme} city {i} draws its exact body at the registered tile");

            var garrison = (i & 1) == 0 ? MapPresence.Present : MapPresence.Absent;
            var annotated = details with { Population = MapFact<int>.Known(0), Garrison = MapFact<MapPresence>.Known(garrison) };
            Render(city with { Details = annotated });
            var faction = catalog.Factions[details.Owner.Value];
            string flag = garrison == MapPresence.Present ? faction.BrightFlag : faction.DarkFlag;
            ArtPoint population = theme switch
            {
                "classic" => new(15, 9),
                "native" when i == 47 => new(22, 6),
                _ => catalog.Cities[i].Population
            };
            ArtPoint flagAt = theme == "classic" ? new(42, 7) : new(48, 4);
            PaintObject(canvas, textures, flag, bodyX + flagAt.X, bodyY + flagAt.Y);
            int x = bodyX + population.X, y = bodyY + population.Y;
            var badge = new SKRectI(x, y, x + 12, y + 12);
            var owner = Opaque(replacement?.Color(details.Owner.Value, faction.Rgb) ?? faction.Rgb);
            using (var pixels = actual.PeekPixels())
            {
                var exact = new HashSet<uint>();
                for (int py = y; py < y + 12; py++)
                    for (int px = x; px < x + 12; px++)
                        exact.Add((uint)pixels.GetPixelColor(px, py));
                Require(exact.Contains((uint)owner) && exact.Count > 2, $"{theme} city {i} shows a populated color badge");
            }
            Require(DifferencesOutside(actual, expected, width, height, badge) == 0 &&
                DifferentMapPixels(ReadMapPixels(actual), ReadMapPixels(expected)) > 0,
                $"{theme} city {i} combines the expected flag and its exact population anchor");
            using (var snapshot = actual.Snapshot())
                sheet.Canvas.DrawImage(snapshot, (i % 8) * width, (i / 8) * height, new SKSamplingOptions(SKFilterMode.Nearest));
            receipts.Add(new { i, key, flag, flagAt, population, details.Owner, digest = Pixels(actual) });
        }

        var known = board.Cities[2] with { Position = default, Name = new("Marsh Gate"u8.ToArray()) };
        var fallbackDetails = known.Details! with
        {
            Knowledge = CityKnowledge.Remembered,
            Appearance = MapFact<MapCityAppearance>.Withheld,
            Population = MapFact<int>.Known(0),
            Garrison = MapFact<MapPresence>.Known(MapPresence.Present),
            Disorder = MapFact<MapPresence>.Known(MapPresence.Present)
        };
        var fallback = known with { Details = fallbackDetails, Size = 0 };
        string inspection = Render(fallback);
        string visible = Pixels(actual);
        Require(inspection.Contains("Marsh Gate", StringComparison.Ordinal) &&
            inspection.Contains("Remembered population 0", StringComparison.Ordinal) &&
            inspection.Contains("Garrison present", StringComparison.Ordinal) &&
            inspection.Contains("Disorder", StringComparison.Ordinal), "withheld appearance leaves all admitted city facts inspectable");
        string fallbackImage = $"MV16-{theme}-withheld-appearance.png";
        Encode(actual, Path.Combine(output, fallbackImage));
        Require(Render(fallback with { Size = 25 }) == inspection && Pixels(actual) == visible,
            "an unadmitted size does not alter a generic city body, badge or inspection");
        foreach (var hidden in new[]
        {
            fallbackDetails with { Population = MapFact<int>.Withheld },
            fallbackDetails with { Garrison = MapFact<MapPresence>.Withheld },
            fallbackDetails with { Disorder = MapFact<MapPresence>.Withheld }
        })
        {
            Render(fallback with { Details = hidden });
            Require(Pixels(actual) != visible, "each independently admitted city annotation contributes pixels with withheld appearance");
        }
        Require(Render(fallback with { Details = null }).StartsWith("Remembered city site", StringComparison.Ordinal),
            "truly anonymous city sites report no admitted identity");
        var remembered = known with
        {
            Size = 23,
            Details = known.Details! with
            {
                Knowledge = CityKnowledge.Remembered,
                Population = MapFact<int>.Known(4),
                Garrison = MapFact<MapPresence>.Withheld
            }
        };
        string rememberedText = Render(remembered), rememberedPixels = Pixels(actual);
        Require(rememberedText.Contains("Remembered population 4", StringComparison.Ordinal) &&
            rememberedText.Contains("Garrison withheld", StringComparison.Ordinal) &&
            Render(remembered with { Size = 27 }) == rememberedText && Pixels(actual) == rememberedPixels,
            "a disclosed body keeps stale remembered population and respects withheld garrison");
        var revealed = remembered with
        {
            Details = remembered.Details! with
            {
                Knowledge = CityKnowledge.AuthorizedReveal,
                Population = MapFact<int>.Known(23)
            }
        };
        string revealedText = Render(revealed);
        Require(revealedText.Contains("Population 23", StringComparison.Ordinal) &&
            !revealedText.Contains("Remembered population", StringComparison.Ordinal) && Pixels(actual) != rememberedPixels,
            "authorized reveal replaces remembered population with its explicitly admitted size");
        string file = $"MV15-{theme}-city-art.png";
        Encode(sheet, Path.Combine(output, file));
        File.WriteAllText(Path.Combine(output, $"MV15-MV16-{theme}.json"), JsonSerializer.Serialize(new
        {
            width,
            height,
            density = 1,
            camera,
            map.Identity,
            theme,
            receipts,
            cityArt = new { file, sha256 = FileHash(Path.Combine(output, file)) },
            withheld = new { file = fallbackImage, sha256 = FileHash(Path.Combine(output, fallbackImage)), inspection },
            disclosures = new { remembered = rememberedText, revealed = revealedText }
        }, Json));

        string Render(ObservedMapCity value)
        {
            var scene = new MapScene(map with { Cities = new([value]) });
            painter.Draw(actual.Canvas, painter.SetScene(scene), camera, width, height, 1, null);
            return MapInspection.Describe(scene, new(default, MapPickKind.City));
        }
    }

    private static SKColor Opaque(uint rgb) => new(0xff000000 | rgb);

    private static void LeftShieldDamageChecks(AssetAtlas atlas, PinnedFonts fonts, string contentCases, string output)
    {
        const int width = 640, height = 460;
        string source = Path.Combine(contentCases, "native", "unit-32.png");
        string pin = FileHash(source);
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            format = "openciv2-map-art/1",
            images = new Dictionary<string, object>
            {
                ["unit/32"] = new { path = "unit-32.png", sha256 = pin, marker = new[] { -16, -16 } }
            }
        });
        var selection = MapArtSelection.Load(path => path == "map-art.json" ? new MemoryStream(manifest) : File.OpenRead(source));
        var actor = new ObservedMapUnit(new(6, 4), new(32), new(1), new(new(1), new(101)), MapUnitDetails.Withheld);
        var other = actor with { Type = new(3), Target = new(new(2), new(102)) };
        var board = MapGallery.Open("MV10").Map with { Cities = new([]), Units = new([actor, other]), Annotations = MapAnnotations.Stock };
        var camera = new MapCamera(-340, -76, 3);
        using var incremental = new MapPainter(atlas, fonts, selection);
        using var fresh = new MapPainter(atlas, fonts, selection);
        using var actual = SKSurface.Create(new SKImageInfo(width, height));
        using var reference = SKSurface.Create(new SKImageInfo(width, height));
        incremental.Draw(actual.Canvas, incremental.SetScene(new(board)), camera, width, height, 1, null);
        var before = ReadMapPixels(actual);
        var settled = board with { Units = new([actor]) };
        var drawn = incremental.Draw(actual.Canvas, incremental.SetScene(new(settled)), camera, width, height, 1, null);
        fresh.Draw(reference.Canvas, fresh.SetScene(new(settled)), camera, width, height, 1, null);
        Require(drawn.Redraw == MapRedraw.Dirty && Pixels(actual) == Pixels(reference),
            "removing a left-offset replacement stack clears its full old shield overhang");
        var after = ReadMapPixels(actual);
        int formerLeftEdge = (int)(camera.ScreenX(actor.Position.X) - 48 * camera.Zoom - 3);
        int beyondFormerDamage = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < formerLeftEdge; x++)
                if (before[y * width + x] != after[y * width + x]) beyondFormerDamage++;
        Require(beyondFormerDamage > 0, "the authored shadow extends beyond the old fixed damage bounds");
        string file = "MV29-left-shield-dirty.png";
        Encode(actual, Path.Combine(output, file));
        File.WriteAllText(Path.Combine(output, "MV29-left-shield-dirty.json"), JsonSerializer.Serialize(new
        {
            width,
            height,
            camera,
            board.Identity,
            pin,
            marker = new ArtPoint(-16, -16),
            beyondFormerDamage,
            damagePixels = drawn.DamagePixels,
            file,
            sha256 = FileHash(Path.Combine(output, file))
        }, Json));
    }

    private static void PaintObject(SKCanvas canvas, MapTextures textures, string key, int x, int y, SKColor? tint = null)
    {
        ArtPoint size = textures.Logical(key);
        canvas.DrawImage(textures.Get(key, 1, tint), SKRect.Create(x, y, size.X, size.Y), new SKSamplingOptions(SKFilterMode.Linear));
    }

    private static int DifferencesOutside(SKSurface actual, SKSurface expected, int width, int height, SKRectI excluded)
    {
        var a = ReadMapPixels(actual);
        var b = ReadMapPixels(expected);
        int changes = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (!excluded.Contains(x, y) && a[y * width + x] != b[y * width + x]) changes++;
        return changes;
    }
}
