using System.Runtime.InteropServices;
using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void PinnedTerrainPixels(AssetAtlas atlas, PinnedFonts fonts, MapArtSelection? replacement, string output)
    {
        const int width = 96, height = 56;
        var camera = new MapCamera(width / 2, height / 2, 1);
        var map = MapGallery.Open("MV01").Map with { Width = 2, Height = 1 };
        var catalog = MapArtCatalog.Stock;
        using var painter = new MapPainter(atlas, fonts, replacement);
        using var textures = new MapTextures(atlas, replacement);
        using var actual = SKSurface.Create(new SKImageInfo(width, height));
        using var reference = SKSurface.Create(new SKImageInfo(width, height));
        using var sheet = SKSurface.Create(new SKImageInfo(width * 11, height * 3));
        var samples = new List<object>();
        var visibleTerrain = new HashSet<string>(StringComparer.Ordinal);
        string[] relief = ["forest", "hills", "mountains"];
        string id = replacement is null ? "stock" : "replacement";
        for (byte terrain = 0; terrain < 11; terrain++)
        {
            var tile = new ObservedTile(terrain, default, false, 0, false);
            var terrainKey = $"terrain/{terrain}";
            var ground = new List<(string Binding, ArtPoint At)> { (terrainKey, default) };
            if (terrain == 10)
                for (int quadrant = 0; quadrant < 4; quadrant++)
                    ground.Add(($"coast/{quadrant}/0", catalog.Coasts[quadrant].Offset));
            else if (terrain is >= 3 and <= 5)
                ground.Add(($"feature/{relief[terrain - 3]}/0", default));

            Draw(tile, ground, 0);
            string bare = Pixels(actual);
            Require(visibleTerrain.Add(bare), $"{id} {terrainKey} is visible and distinct in isolation");
            if (replacement is not null)
                Require(replacement.Image(terrainKey) is not null, $"selected sheet replaces {terrainKey}");

            bool connects = terrain != 10;
            var featureTile = tile with { River = connects, Improvements = connects ? MapImprovements.Road : default };
            var network = new List<(string Binding, ArtPoint At)>(ground);
            if (connects) network.AddRange([("river/0", default), ("road/0", default)]);
            Draw(featureTile, network);
            var withoutSpecial = ReadMapPixels(reference);
            string? first = null;
            for (byte resource = 1; resource <= 2; resource++)
            {
                string key = $"resource/{terrain * 2 + resource - 1}";
                VerifySpecial(featureTile with { Resource = resource }, key, resource);
                string image = Pixels(actual);
                if (resource == 1) first = image;
                else
                {
                    bool samePinnedImage = catalog.Images[key].Sha256 == catalog.Images[$"resource/{terrain * 2}"].Sha256;
                    Require((first == image) == (replacement is null && samePinnedImage),
                        $"{id} {terrainKey} special identities follow their admitted art");
                }
            }
            if (terrain == 2)
                VerifySpecial(featureTile with { GrasslandShield = true }, "resource/shield", null);

            void VerifySpecial(ObservedTile variant, string key, int? row)
            {
                var layers = new List<(string Binding, ArtPoint At)>(network) { (key, default) };
                Draw(variant, layers, row);
                int feature = DifferentMapPixels(ReadMapPixels(actual), withoutSpecial);
                // The four stock Ocean quadrants cover its underlying base
                // tile. Their visible water is the support for Ocean resources.
                Paint(reference, layers.Skip(terrain == 10 ? ground.Count : 1));
                int baseArt = DifferentMapPixels(ReadMapPixels(actual), ReadMapPixels(reference));
                Require(feature > 0 && baseArt > 0, $"{id} {key} and its underlying {terrainKey} both contribute visible pixels");
                samples.Add(new { terrain, resource = key, visibleResourcePixels = feature, visibleTerrainPixels = baseArt });
            }

            void Draw(ObservedTile variety, List<(string Binding, ArtPoint At)> layers, int? row = null)
            {
                long epoch = painter.SetScene(new(map with { Tiles = new([variety]) }));
                painter.Draw(actual.Canvas, epoch, camera, width, height, 1, null);
                Paint(reference, layers);
                Require(Pixels(actual) == Pixels(reference), $"{id} composition matches {string.Join(", ", layers.Select(item => item.Binding))}");
                if (row is int y)
                {
                    using var image = actual.Snapshot();
                    sheet.Canvas.DrawImage(image, terrain * width, y * height, new SKSamplingOptions(SKFilterMode.Nearest));
                }
            }
        }
        string file = $"MV01-MV05-{id}-pixels.png";
        Encode(sheet, Path.Combine(output, file));
        File.WriteAllText(Path.Combine(output, $"MV01-MV05-{id}-pixels.json"),
            JsonSerializer.Serialize(new { id, width, height, density = 1, zoom = 1, file, imageSha256 = FileHash(Path.Combine(output, file)), samples }, Json));

        void Paint(SKSurface surface, IEnumerable<(string Binding, ArtPoint At)> layers)
        {
            surface.Canvas.Clear(new SKColor(0xff172b33));
            foreach (var (binding, at) in layers)
                PaintGroundTile(surface.Canvas, textures, binding, width / 2 - 32 + at.X, height / 2 - 16 + at.Y);
        }
    }

    private static void PinnedImprovementPixels(AssetAtlas atlas, PinnedFonts fonts, MapArtSelection? replacement, string output)
    {
        const int width = 96, height = 76;
        var camera = new MapCamera(width / 2, height - 24, 1);
        var empty = new ObservedTile(2, default, false, 0, false);
        var pollution = empty with { Improvements = MapImprovements.Pollution };
        var fortress = empty with { Improvements = MapImprovements.Fortification };
        var airbase = empty with { Improvements = MapImprovements.CitySite | MapImprovements.Fortification };
        var map = MapGallery.Open("MV01").Map with { Width = 2, Height = 1 };
        var figure = MapGallery.Open("MV08").Map.Units[0] with { Position = new(0, 0), Details = MapUnitDetails.Withheld };
        var fortified = figure with { Details = figure.Details with { Order = MapFact<UnitOrder>.Known(UnitOrder.Fortified) } };
        using var painter = new MapPainter(atlas, fonts, replacement);
        using var textures = new MapTextures(atlas, replacement);
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        using var reference = SKSurface.Create(new SKImageInfo(width, height));
        using var sheet = SKSurface.Create(new SKImageInfo(width * 8, height * 2));
        var rows = new List<object>();
        string theme = replacement is null ? "stock" : "replacement";
        var baseline = Render(empty);
        var village = empty with { Hut = MapFact<MapPresence>.Known(MapPresence.Present) };
        Check(village, "improvement/village", tall: false);
        Check(pollution, "improvement/pollution", tall: false);
        Check(fortress, "improvement/fortress", tall: true);
        var emptyAirfield = Check(airbase with { AirbaseOccupant = MapFact<MapTileOwnership>.Known(new(null)) },
            "improvement/airbase", tall: true);
        Require(DifferentMapPixels(Render(airbase with { AirbaseOccupant = MapFact<MapTileOwnership>.Withheld }), emptyAirfield) == 0,
            "withheld occupancy does not publish an aircraft or a city marker");
        Require(DifferentMapPixels(Render(empty with { Hut = MapFact<MapPresence>.Withheld }), baseline) == 0,
            "withheld hut presence does not publish a hut mark");
        var ownerArt = new HashSet<string>(StringComparer.Ordinal);
        for (byte role = 0; role < 8; role++)
        {
            var admitted = airbase with { AirbaseOccupant = MapFact<MapTileOwnership>.Known(new(new(role))) };
            string binding = MapArtCatalog.Stock.Factions[role].OccupiedAirbase;
            Check(admitted, binding, tall: true);
            Require(ownerArt.Add(Pixels(surface)), $"{theme} occupied airbase role {role} is distinct");
        }
        var withFortified = Render(fortress, fortified);
        var withoutFortified = Render(fortress, figure);
        var art = MapPainter.UnitBinding(figure);
        var marker = textures.Marker(art.Body, art.Shield);
        // Changing the order changes its badge as well as the sprite.
        // Compare sprite pixels outside the independent status rectangle.
        int statusX = (int)camera.X - 32 + marker.X, statusY = (int)camera.Y - 32 + marker.Y + 6;
        int fortificationPixels = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (x >= statusX && x < statusX + 12 && y >= statusY && y < statusY + 9) continue;
                int pixel = y * width + x;
                if (withFortified[pixel] != withoutFortified[pixel]) fortificationPixels++;
            }
        int fortressPixels = DifferentMapPixels(withFortified, Render(empty, fortified));
        Require((fortificationPixels == 0) == FullyCovered("improvement/fortified", art.Body) &&
            (fortressPixels == 0) == FullyCovered("improvement/fortress", art.Body, "improvement/fortified"),
            $"{theme} fortified figure and fortress respect the foreground alpha geometry ({fortificationPixels}, {fortressPixels})");
        Render(fortress, fortified);
        AppendCapture("fortified-unit-over-fortress", checkedPixels: fortificationPixels + fortressPixels);
        string file = $"MV08-{theme}-pixels.png";
        Encode(sheet, Path.Combine(output, file));
        File.WriteAllText(Path.Combine(output, $"MV08-{theme}-pixels.json"),
            JsonSerializer.Serialize(new
            {
                theme,
                zoom = 1,
                density = 1,
                width,
                height,
                file,
                imageSha256 = FileHash(Path.Combine(output, file)),
                fortificationPixels,
                fortressPixels,
                rows
            }, Json));

        uint[] Check(ObservedTile tile, string key, bool tall)
        {
            var actual = Render(tile);
            reference.Canvas.Clear(new SKColor(0xff172b33));
            int x = width / 2 - 32, y = (int)camera.Y - 16;
            PaintGroundTile(reference.Canvas, textures, "terrain/2", x, y);
            if (tall)
            {
                var image = textures.Get(key);
                var logical = textures.Logical(key);
                reference.Canvas.DrawImage(image, SKRect.Create(x, y - 16, logical.X, logical.Y), new SKSamplingOptions(SKFilterMode.Linear));
            }
            else PaintGroundTile(reference.Canvas, textures, key, x, y);
            Require(DifferentMapPixels(actual, ReadMapPixels(reference)) == 0, $"{theme} {key} exactly matches the pinned composition");
            int contribution = DifferentMapPixels(actual, baseline);
            Require(contribution > 0, $"{theme} {key} contributes visible pixels");
            if (replacement is not null) Require(replacement.Image(key) is not null, $"{key} is supplied by the selected classic sheet");
            AppendCapture(key, contribution);
            return actual;
        }

        uint[] Render(ObservedTile tile, ObservedMapUnit? unit = null)
        {
            var observation = map with { Tiles = new([tile]), Units = unit is { } value ? new([value]) : new([]) };
            long epoch = painter.SetScene(new(observation));
            var stats = painter.Draw(surface.Canvas, epoch, camera, width, height, 1, null);
            Require(stats.Objects == (unit is null ? 0 : 1), "improvement cases do not introduce a visible city");
            return ReadMapPixels(surface);
        }

        void AppendCapture(string binding, int checkedPixels)
        {
            using var image = surface.Snapshot();
            int column = rows.Count % 8, row = rows.Count / 8;
            sheet.Canvas.DrawImage(image, column * width, row * height, new SKSamplingOptions(SKFilterMode.Nearest));
            rows.Add(new { binding, checkedPixels, column, row });
        }

        bool FullyCovered(string lower, string upper, string? middle = null)
        {
            using var below = textures.Get(lower).PeekPixels();
            using var above = textures.Get(upper).PeekPixels();
            using var between = middle is null ? null : textures.Get(middle).PeekPixels();
            for (int y = 0; y < below.Height; y++)
                for (int x = 0; x < below.Width; x++)
                    if (below.GetPixelColor(x, y).Alpha > 0 && above.GetPixelColor(x, y).Alpha != 255 &&
                        between?.GetPixelColor(x, y).Alpha != 255 && (x < 2 || y < 2 || above.GetPixelColor(x - 2, y - 2).Alpha != 255))
                        return false;
            return true;
        }
    }

    private static void PaintGroundTile(SKCanvas canvas, MapTextures textures, string key, float x, float y, double zoom = 1)
    {
        var image = textures.Get(key, zoom);
        var logical = textures.Logical(key);
        float scale = (float)(logical.X * zoom / image.Width);
        canvas.DrawAtlas(image, [SKRect.Create(image.Width, image.Height)], [new(scale, 0, x, y)],
            new SKSamplingOptions(scale > 1 ? SKFilterMode.Nearest : SKFilterMode.Linear));
    }

    private static uint[] ReadMapPixels(SKSurface surface)
    {
        using var pixels = surface.PeekPixels();
        return MemoryMarshal.Cast<byte, uint>(pixels.GetPixelSpan()).ToArray();
    }

    private static int DifferentMapPixels(uint[] first, uint[] second)
    {
        int changed = 0;
        for (int i = 0; i < first.Length; i++)
            if (first[i] != second[i]) changed++;
        return changed;
    }
}
