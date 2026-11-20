using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void GroundNetworkPixelChecks(AssetAtlas atlas, PinnedFonts fonts, MapArtSelection? replacement, string output)
    {
        const int columns = 7, rows = 10;
        var relief = MapGallery.Open("MV03").Map;
        var rivers = MapGallery.Open("MV04").Map;
        rivers = rivers with { Tiles = new(rivers.Tiles.Select(tile => tile with { Improvements = default })) };
        string theme = replacement is null ? "stock" : "classic";
        var receipts = new List<object>();
        using var painter = new MapPainter(atlas, fonts, replacement);
        using var textures = new MapTextures(atlas, replacement);

        var reliefKeys = Check("MV03", "all-variants", relief, 1880, 1420, new(46, 38, 1), TerrainPass.Blend, TerrainPass.Relief);
        Require(reliefKeys.Count(key => key.StartsWith("dither/", StringComparison.Ordinal)) == 44 &&
            reliefKeys.Count(key => key.StartsWith("feature/", StringComparison.Ordinal)) == 48,
            "all 44 edge blends and all 48 relief masks appear in the independent placement plan");
        var reliefEdge = Seam(
            (0, 4, 3, false), (13, 3, 3, false), (1, 3, 4, false), (13, 5, 5, false),
            (1, 5, 5, false), (2, 4, 4, false), (3, 5, 3, false), (12, 4, 3, false));
        Check("MV03", "relief-wrap", reliefEdge, 560, 230, new(68, 45, 1), TerrainPass.Blend, TerrainPass.Relief);
        Check("MV03", "relief-flat", reliefEdge with { Flat = true }, 560, 230, new(68, 45, 1), TerrainPass.Blend, TerrainPass.Relief);

        foreach (double zoom in new[] { 1d, .5 })
        {
            string band = zoom == 1 ? "full" : "reduced";
            var keys = Check("MV04", "all-rivers-" + band, rivers, (int)(1840 * zoom), (int)(540 * zoom),
                new(40 * zoom, 40 * zoom, zoom), TerrainPass.River);
            Require(keys.Count(key => key.StartsWith("river/", StringComparison.Ordinal)) == 16 &&
                keys.Count(key => key.StartsWith("mouth/", StringComparison.Ordinal)) == 4,
                "all river junctions and mouth directions appear at each detail setting");
            var mouthEdge = Seam(
                (0, 4, 10, false), (13, 3, 1, true), (1, 3, 1, true), (1, 5, 1, true),
                (2, 6, 1, true), (4, 6, 10, false), (5, 5, 10, false), (5, 7, 10, false));
            Check("MV04", "mouth-wrap-" + band, mouthEdge, (int)(560 * zoom), (int)(240 * zoom),
                new(56 * zoom, 40 * zoom, zoom), TerrainPass.River);
            Check("MV04", "mouth-flat-" + band, mouthEdge with { Flat = true }, (int)(560 * zoom), (int)(240 * zoom),
                new(56 * zoom, 40 * zoom, zoom), TerrainPass.River);
        }
        File.WriteAllText(Path.Combine(output, $"MV03-MV04-{theme}.json"), JsonSerializer.Serialize(receipts, Json));

        MapObservation Seam(params (int X, int Y, byte Terrain, bool River)[] cells)
        {
            var tiles = Enumerable.Repeat(new ObservedTile(1, default, false, 0, false), columns * rows).ToArray();
            foreach (var (x, y, terrain, river) in cells)
                tiles[y * columns + x / 2] = new(terrain, default, river, 0, false);
            return relief with { Width = columns * 2, Height = rows, Flat = false, Tiles = new(tiles) };
        }

        HashSet<string> Check(string id, string name, MapObservation map, int width, int height, MapCamera camera, params TerrainPass[] visiblePasses)
        {
            using var actual = SKSurface.Create(new SKImageInfo(width, height));
            using var reference = SKSurface.Create(new SKImageInfo(width, height));
            painter.Draw(actual.Canvas, painter.SetScene(new(map)), camera, width, height, 1, null);
            var plan = PlanTerrainReference(map, camera, width);
            DrawTerrainReference(reference.Canvas, textures, plan, camera);
            string filename = $"{id}-{theme}-{name}.png";
            Encode(actual, Path.Combine(output, filename));
            var pixels = ReadMapPixels(actual);
            int differs = DifferentMapPixels(pixels, ReadMapPixels(reference));
            if (differs != 0) Encode(reference, Path.Combine(output, $"{id}-{theme}-{name}-reference.png"));
            Require(differs == 0, $"{theme} {name} matches unchunked pinned composition ({differs} differing pixels)");
            var evidence = new Dictionary<string, int>();
            foreach (var pass in visiblePasses)
            {
                DrawTerrainReference(reference.Canvas, textures, plan.Where(item => item.Pass != pass), camera);
                int contribution = DifferentMapPixels(pixels, ReadMapPixels(reference));
                Require(contribution > 0, $"{theme} {name} has a visible contribution from {pass}");
                evidence.Add(pass.ToString(), contribution);
            }
            var keys = plan.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
            receipts.Add(new
            {
                id,
                name,
                theme,
                width,
                height,
                density = 1,
                camera,
                map.Flat,
                map.Identity,
                keys = keys.Order().ToArray(),
                visiblePixels = evidence,
                filename,
                sha256 = FileHash(Path.Combine(output, filename))
            });
            return keys;
        }
    }

    private static void AgriculturalPixelChecks(AssetAtlas atlas, PinnedFonts fonts, MapArtSelection? replacement, string output)
    {
        const int columns = 5, rows = 9, width = 350, height = 180;
        const MapFeatures farmland = MapFeatures.Irrigation | MapFeatures.Mine;
        var center = new MapPoint(4, 4);
        var southeast = new MapPoint(5, 5);
        var southwest = new MapPoint(3, 5);
        var plain = new ObservedTile(1, default, false, 0, false);
        var authored = MapGallery.Open("MV06").Map;
        var first = authored.Cities[0];
        var city = first with
        {
            Position = center,
            Name = "",
            Details = first.Details! with
            {
                Appearance = MapFact<MapCityAppearance>.Known(new(0, 0, false)),
                Population = MapFact<int>.Withheld,
                Garrison = MapFact<MapPresence>.Withheld,
                Disorder = MapFact<MapPresence>.Withheld
            }
        };
        var camera = new MapCamera(42, 26, 1);
        string theme = replacement is null ? "stock" : "classic";
        using var painter = new MapPainter(atlas, fonts, replacement);
        using var textures = new MapTextures(atlas, replacement);
        using var image = SKSurface.Create(new SKImageInfo(width, height));
        using var reference = SKSurface.Create(new SKImageInfo(width, height));
        var receipts = new List<object>();

        var markers = new (string Name, MapFeatures State, TerrainPlacement? Sprite)[]
        {
            ("none", default, null),
            ("irrigation", MapFeatures.Irrigation, new(TerrainPass.Field, "improvement/irrigation", center)),
            ("farmland", farmland, new(TerrainPass.Field, "improvement/farmland", center)),
            ("mine", MapFeatures.Mine, new(TerrainPass.Mine, "improvement/mine", center))
        };
        var combinations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, state, sprite) in markers)
        {
            Require(combinations.Add(Check(name, plain with { Improvements = state }, sprite)),
                $"{theme} {name} is visually distinct on the same plains terrain");
            Check(name + "-river-resource", plain with { Improvements = state, River = true, Resource = 2 }, sprite);
            Check(name + "-city", plain with { Improvements = state | MapFeatures.CitySite, River = true, Resource = 2 },
                sprite, showCity: true);
        }
        var centerCity = plain with { Improvements = MapFeatures.CitySite, River = true, Resource = 2 };
        foreach (var direction in new[] { (Name: "southeast", Point: southeast), (Name: "southwest", Point: southwest) })
        {
            var field = new TerrainPlacement(TerrainPass.Field, "improvement/farmland", direction.Point);
            Check("underlay-" + direction.Name, centerCity, markers[2].Sprite, true,
                (direction.Point, plain with { Improvements = farmland }, field));
            Check("hidden-" + direction.Name, centerCity, null, true,
                (direction.Point, plain with { Terrain = null, Improvements = farmland }, null));
        }
        var bareTile = centerCity with { Improvements = MapFeatures.CitySite | farmland | MapFeatures.Pollution };
        Check("bare", bareTile, null, showCity: true, bare: true);
        File.WriteAllText(Path.Combine(output, $"MV06-agriculture-{theme}.json"), JsonSerializer.Serialize(receipts, Json));

        string Check(string name, ObservedTile tile, TerrainPlacement? agriculture, bool showCity = false,
            (MapPoint Point, ObservedTile Tile, TerrainPlacement? Art)? neighbor = null, bool bare = false)
        {
            var tiles = Enumerable.Repeat(plain, columns * rows).ToArray();
            tiles[Index(center)] = tile;
            if (neighbor is { } adjacent) tiles[Index(adjacent.Point)] = adjacent.Tile;
            var map = authored with
            {
                Width = columns * 2,
                Height = rows,
                Tiles = new(tiles),
                Units = new([]),
                Cities = showCity ? new([city]) : new([])
            };
            var scene = new MapScene(map);
            var sprites = new List<TileSprite>();
            MapTileArt.Select(sprites, scene, center, MapDetail.Full, bare);
            var agriculturalKeys = sprites.Where(item => item.Layer is TileArtLayer.Agriculture or TileArtLayer.Mine)
                .Select(item => item.Key);
            Require(agriculturalKeys.SequenceEqual(agriculture is { } selected ? [selected.Key] : []),
                $"{theme} {name} selects the expected single field or mine");
            painter.Draw(image.Canvas, painter.SetScene(scene), camera, width, height, 1, null, new(Bare: bare));
            var plan = PlanTerrainReference(map, camera, width);
            if (agriculture is { } field) plan.Add(field);
            if (neighbor?.Art is { } neighborField) plan.Add(neighborField);
            if (tile.Resource != 0) plan.Add(new(TerrainPass.Resource, "resource/3", center));
            if ((tile.Improvements & MapFeatures.Pollution) != 0)
                plan.Add(new(TerrainPass.Pollution, "improvement/pollution", center));
            DrawExpected(plan);
            var current = ReadMapPixels(image);
            Require(DifferentMapPixels(current, ReadMapPixels(reference)) == 0, $"{theme} {name} matches its exact field, river, resource and city layers");
            Require(scene.Tile(center) == tile, "neighborhood underlay keeps the published city improvement unchanged");
            int fieldPixels = 0;
            int fieldPixelsBeforeOverlays = 0;
            if (agriculture is { } visible)
            {
                DrawExpected(plan.Where(item => item != visible));
                fieldPixels = DifferentMapPixels(current, ReadMapPixels(reference));
                var beforeOverlays = plan.Where(item => item.Pass <= visible.Pass);
                DrawTerrainReference(reference.Canvas, textures, beforeOverlays, camera);
                var withField = ReadMapPixels(reference);
                DrawTerrainReference(reference.Canvas, textures, beforeOverlays.Where(item => item != visible), camera);
                fieldPixelsBeforeOverlays = DifferentMapPixels(withField, ReadMapPixels(reference));
                Require(fieldPixelsBeforeOverlays > 0, $"{theme} {name} draws the field or mine before overlapping features");
                Require(fieldPixels > 0 || (replacement is not null && (tile.River || tile.Resource != 0 || showCity)),
                    $"{theme} {name} retains a visible part of its field or mine");
            }
            string filename = $"MV06-{theme}-{name}.png";
            Encode(image, Path.Combine(output, filename));
            receipts.Add(new
            {
                name,
                theme,
                width,
                height,
                density = 1,
                camera,
                map.Identity,
                tile,
                neighbor,
                bare,
                fieldPixels,
                fieldPixelsBeforeOverlays,
                filename,
                sha256 = FileHash(Path.Combine(output, filename))
            });
            return Pixels(image);

            void DrawExpected(IEnumerable<TerrainPlacement> content)
            {
                DrawTerrainReference(reference.Canvas, textures, content, camera);
                if (showCity && !bare)
                    PaintObject(reference.Canvas, textures, "city/0", (int)camera.ScreenX(center.X) - 32, (int)camera.ScreenY(center.Y) - 32);
            }
        }

        static int Index(MapPoint point) => point.Y * columns + point.X / 2;
    }
}
