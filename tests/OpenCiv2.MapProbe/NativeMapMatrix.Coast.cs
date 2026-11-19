using System.Text.Json;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private enum TerrainPass { Base, Coast, Blend, Relief, Field, River, Resource, Mine, Pollution, Fog }
    private readonly record struct TerrainPlacement(TerrainPass Pass, string Key, MapPoint Position, ArtPoint Offset = default);

    private static void ShorelinePixelChecks(MapPainter painter, AssetAtlas atlas, string output)
    {
        using var textures = new MapTextures(atlas);
        var board = MapGallery.Open("MV02").Map;
        var samples = new List<object>();
        Check("all-quadrants", board, 1920, 864, new(55, 42, 1));

        var land = new ObservedTile(2, default, false, 0, false);
        var sea = land with { Terrain = 10 };
        CheckShape("island", ["~~~~~~", "~~~~~~", "~~~~~~", "~~L~~~", "~~~~~~", "~~~~~~", "~~~~~~"]);
        CheckShape("channel", ["LLL~LL", "LL~LLL", "LL~LLL", "L~LLLL", "L~LLLL", "~LLLLL", "~LLLLL"]);
        CheckShape("peninsula", ["LLLLLL", "~~L~LL", "~~LL~~", "~~~L~~", "~~~L~~", "~~~~~~", "~~~~~~"]);
        string[] westEdge = ["LLLLLL", "L?L~~~", "L~~L~~", "~~~L~~", "~~~L~~", "~~~~~~", "~~LL~~"];
        var flatImage = CheckShape("flat-corner", westEdge);
        var wrappedImage = CheckShape("wrapped-corner", westEdge, flat: false);
        Require(flatImage != wrappedImage, "contacts across the coast seam differ from the physical flat edge");
        File.WriteAllText(Path.Combine(output, "MV02-shoreline-pixels.json"), JsonSerializer.Serialize(samples, Json));

        string CheckShape(string name, string[] pattern, bool flat = true)
        {
            var map = board with
            {
                Flat = flat,
                Width = pattern[0].Length * 2,
                Height = pattern.Length,
                Tiles = new(pattern.SelectMany(row => row.Select(cell => cell switch
                {
                    '~' => sea,
                    'L' => land,
                    _ => default
                })).ToArray())
            };
            return Check(name, map, 480, 180, new(51, 40, 1), pattern);
        }

        string Check(string name, MapObservation map, int width, int height, MapCamera camera, string[]? pattern = null)
        {
            using var actual = SKSurface.Create(new SKImageInfo(width, height));
            using var reference = SKSurface.Create(new SKImageInfo(width, height));
            var scene = new MapScene(map);
            painter.Draw(actual.Canvas, painter.SetScene(scene), camera, width, height, 1, null);
            var plan = PlanTerrainReference(map, camera, width);
            var selectedCoasts = plan.Where(item => item.Pass == TerrainPass.Coast).Select(item => item.Key).ToHashSet();
            DrawTerrainReference(reference.Canvas, textures, plan, camera);
            int differences = DifferentMapPixels(ReadMapPixels(actual), ReadMapPixels(reference));
            if (differences > 0)
                Encode(reference, Path.Combine(output, $"MV02-{name}-reference.png"));
            string file = $"MV02-{name}.png";
            Encode(actual, Path.Combine(output, file));
            Require(differences == 0, $"independent {name} shoreline composition ({differences} differing pixels)");
            if (name == "all-quadrants") Require(selectedCoasts.Count == 32, "reference draws all 32 pinned coast selectors");
            string image = FileHash(Path.Combine(output, file));
            samples.Add(new
            {
                name,
                map.Flat,
                pattern,
                width,
                height,
                density = 1,
                camera,
                file,
                sourceSelectors = selectedCoasts.Order().ToArray(),
                imageSha256 = image
            });
            return image;
        }
    }

    // Independent reference for the pinned ground sheets. Walk the declared
    // lattice and place every pass on an unchunked canvas in map-row order.
    private static List<TerrainPlacement> PlanTerrainReference(MapObservation map, MapCamera camera, int width)
    {
        MapPoint[][] contacts =
        [
            [new(-1, -1), new(0, -2), new(1, -1)],
            [new(1, -1), new(2, 0), new(1, 1)],
            [new(1, 1), new(0, 2), new(-1, 1)],
            [new(-1, 1), new(-2, 0), new(-1, -1)]
        ];
        ArtPoint[] coastOffsets = [new(16, 0), new(32, 8), new(16, 16), new(0, 8)];
        ArtPoint[] edgeOffsets = [new(32, 0), new(32, 16), new(0, 16), new(0, 0)];
        string[] relief = ["forest", "hills", "mountains"];
        var placements = new List<TerrainPlacement>();
        double tileWidth = camera.Zoom * 32;
        int first = map.Flat ? 0 : (int)Math.Floor(-camera.X / tileWidth) - 2;
        int last = map.Flat ? map.Width : (int)Math.Ceiling((width - camera.X) / tileWidth) + 3;
        for (int y = 0; y < map.Height; y++)
            for (int x = first + ((first ^ y) & 1); x < last; x += 2)
            {
                var position = new MapPoint(x, y);
                var center = Observation(x, y);
                placements.Add(new(TerrainPass.Base, center.Terrain is byte id ? $"terrain/{id}" : "fog/full", position));
                if (center.Terrain is not byte terrain) continue;
                int connectivity = 0, river = 0;
                for (int q = 0; q < 4; q++)
                {
                    var direction = contacts[q][2];
                    int nx = x + direction.X, ny = y + direction.Y;
                    var neighbor = Observation(nx, ny);
                    if (terrain == 10)
                    {
                        int mask = 0;
                        for (int i = 0; i < 3; i++)
                        {
                            var contact = contacts[q][i];
                            if (Observation(x + contact.X, y + contact.Y).Terrain is < 10) mask += 1 << i;
                        }
                        placements.Add(new(TerrainPass.Coast, $"coast/{q}/{mask}", position, coastOffsets[q]));
                        if (neighbor.Terrain is not null && neighbor.River)
                            placements.Add(new(TerrainPass.River, $"mouth/{q}", position));
                    }
                    else if (neighbor.Terrain is byte adjacent)
                    {
                        if (adjacent == terrain) connectivity += 1 << q;
                        else if (camera.Zoom >= .35)
                            placements.Add(new(TerrainPass.Blend, $"dither/{(adjacent == 10 ? 12 : adjacent)}/{q}", position, edgeOffsets[q]));
                        if (neighbor.River || adjacent == 10) river += 1 << q;
                    }
                    if (neighbor.Terrain is null && Domain(nx, ny))
                        placements.Add(new(TerrainPass.Fog, $"fog/{q}", position, edgeOffsets[q]));
                }
                if (terrain is >= 3 and <= 5 && camera.Zoom >= .35)
                    placements.Add(new(TerrainPass.Relief, $"feature/{relief[terrain - 3]}/{connectivity}", position));
                if (terrain < 10 && center.River) placements.Add(new(TerrainPass.River, $"river/{river}", position));
            }
        return placements;

        bool Domain(int x, int y) => y >= 0 && y < map.Height && (!map.Flat || x >= 0 && x < map.Width);

        ObservedTile Observation(int x, int y) => Domain(x, y)
            ? map.Tiles[y * (map.Width / 2) + ((x % map.Width + map.Width) % map.Width) / 2]
            : default;
    }

    private static void DrawTerrainReference(SKCanvas canvas, MapTextures textures, IEnumerable<TerrainPlacement> placements, MapCamera camera)
    {
        canvas.Clear(new SKColor(0xff172b33));
        foreach (var item in placements.OrderBy(item => item.Pass))
            PaintGroundTile(canvas, textures, item.Key,
                (float)(camera.ScreenX(item.Position.X) + (item.Offset.X - 32) * camera.Zoom),
                (float)(camera.ScreenY(item.Position.Y) + (item.Offset.Y - 16) * camera.Zoom), camera.Zoom);
    }
}
