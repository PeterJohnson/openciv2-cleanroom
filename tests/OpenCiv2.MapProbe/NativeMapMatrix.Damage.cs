using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void RetainedGroundAtlasChecks(AssetAtlas atlas, PinnedFonts fonts, MapArtSelection replacement)
    {
        const int width = 1920, height = 1080;
        var camera = new MapCamera(-992, -1140, 1);
        var map = MapGallery.Open("Mixed").Map;
        using var retained = new MapPainter(atlas, fonts);
        using var actual = SKSurface.Create(new SKImageInfo(width, height));
        using var expected = SKSurface.Create(new SKImageInfo(width, height));
        retained.Draw(actual.Canvas, retained.SetScene(new(map)), camera, width, height, 1, null);
        for (int step = 0; step < 12; step++)
        {
            if (step == 6)
            {
                retained.SelectArt(replacement);
                Require(retained.PackedGroundBytes == 0, "content replacement releases retained ground sources");
            }
            var tiles = map.Tiles.ToArray();
            foreach (var point in new MapPoint[] { new(62, 108), new(64, 108), new(62, 110), new(64, 110) })
            {
                int index = point.Y * (map.Width / 2) + point.X / 2;
                tiles[index] = tiles[index] with { Terrain = (byte)(step % 11), Improvements = MapFeatures.Road };
            }
            map = map with { Revision = map.Revision + 1, Tiles = new(tiles) };
            var stats = retained.Draw(actual.Canvas, retained.SetScene(new(map)), camera, width, height, 1, null);
            using var fresh = new MapPainter(atlas, fonts, step >= 6 ? replacement : null,
                parallelForeground: false, packGroundTextures: false);
            fresh.Draw(expected.Canvas, fresh.SetScene(new(map)), camera, width, height, 1, null);
            Require(stats.Error is null && Pixels(actual) == Pixels(expected) &&
                retained.PackedGroundBytes <= 4L * 1024 * 1024 && stats.GroundCacheBytes <= stats.GroundCacheLimitBytes,
                "retained ground atlas matches unpacked fresh terrain and stays within its budget");
            // Classic replacements can use their original unpremultiplied
            // pixels and take the unpacked fallback. Require reuse for stock
            // art; both paths must satisfy the exactness and budget checks.
            if (OperatingSystem.IsBrowser() && step == 5)
            {
                Require(retained.PackedGroundBytes > 0,
                    $"1080p browser dirty updates reuse packed ground sources: step={step}, chunks={stats.PreparedChunks}");
            }
        }
        map = map with { Identity = map.Identity with { Observer = new(2) }, Units = new([]), Cities = new([]) };
        retained.SetScene(new(map));
        Require(retained.PackedGroundBytes == 0, "observer replacement releases retained ground sources");
    }

    // P02 MV29: changes outside the viewport can still affect edge sprites,
    // neighboring joins, labels and a later pan into previously cached ground.
    private static void ViewportDamageCoverageChecks(AssetAtlas atlas, PinnedFonts fonts)
    {
        OffscreenPathDamageChecks(atlas, fonts);
        const int width = 640, height = 360;
        var authored = MapGallery.Open("Mixed").Map;
        foreach (bool flat in new[] { false, true })
            foreach (double zoom in new[] { .5, 1.25, 3 })
                foreach (int density in new[] { 1, 2 })
                {
                    var camera = new MapCamera(flat ? -32 * 64 * zoom : 24, -16 * 64 * zoom, zoom);
                    var farCamera = camera with { Y = -16 * 200 * zoom };
                    var map = authored with
                    {
                        Flat = flat,
                        Tiles = new(Enumerable.Repeat(new ObservedTile(1, MapFeatures.Road, false, 0, false), authored.Tiles.Count)),
                        Units = new([]),
                        Cities = new([]),
                        Annotations = MapAnnotations.Stock
                    };
                    MapPoint Point(double x, double y)
                    {
                        int row = (int)Math.Round((y - camera.Y) / (16 * zoom));
                        int column = (int)Math.Round((x - camera.X) / (32 * zoom));
                        column += (column ^ row) & 1;
                        return new(MapCamera.CanonicalX(column, map.Width), row);
                    }
                    var edges = new[] { Point(-32 * zoom, 80), Point(width + 32 * zoom, 160),
                Point(160, -16 * zoom), Point(320, height + 16 * zoom) };
                    map = map with
                    {
                        Units = new(edges.Select((point, i) => authored.Units[i] with { Position = point })),
                        Cities = new([authored.Cities[0] with { Position = Point(500, 220), Name = "Edge label" }])
                    };
                    var tiles = map.Tiles.ToArray();
                    foreach (var point in edges.Append(new MapPoint(64, 202)))
                    {
                        int index = point.Y * (map.Width / 2) + point.X / 2;
                        tiles[index] = tiles[index] with { Terrain = 4, Improvements = MapFeatures.Railroad };
                    }
                    var changed = map with
                    {
                        Revision = map.Revision + 1,
                        Tiles = new(tiles),
                        Units = new([]),
                        Cities = new([map.Cities[0] with { Name = "A changed label that extends beyond its city" }])
                    };
                    using var retained = new MapPainter(atlas, fonts);
                    using var fresh = new MapPainter(atlas, fonts);
                    using var actual = SKSurface.Create(new SKImageInfo(width * density, height * density));
                    using var expected = SKSurface.Create(new SKImageInfo(width * density, height * density));
                    actual.Canvas.Scale(density);
                    expected.Canvas.Scale(density);
                    long initial = retained.SetScene(new(map));
                    retained.Draw(actual.Canvas, initial, farCamera, width, height, density, null);
                    retained.Draw(actual.Canvas, initial, camera, width, height, density, null);
                    long updated = retained.SetScene(new(changed));
                    var result = retained.Draw(actual.Canvas, updated, camera, width, height, density, null);
                    fresh.Draw(expected.Canvas, fresh.SetScene(new(changed)), camera, width, height, density, null);
                    Require(result.Error is null && result.DamageComparedTiles > 0 &&
                        result.DamageComparedTiles < map.Tiles.Count / 2 && Pixels(actual) == Pixels(expected),
                        $"viewport damage includes edge overhangs and labels at flat={flat}, zoom={zoom}, density={density}");
                    retained.Draw(actual.Canvas, updated, farCamera, width, height, density, null);
                    using var farFresh = new MapPainter(atlas, fonts);
                    farFresh.Draw(expected.Canvas, farFresh.SetScene(new(changed)), farCamera, width, height, density, null);
                    Require(Pixels(actual) == Pixels(expected), "offscreen cached ground is invalidated before the next pan");
                }
    }

    private static void OffscreenPathDamageChecks(AssetAtlas atlas, PinnedFonts fonts)
    {
        const int width = 640, height = 360;
        var map = MapGallery.Open("Mixed").Map;
        map = map with
        {
            Flat = true,
            Tiles = new(Enumerable.Repeat(new ObservedTile(1, default, false, 0, false), map.Tiles.Count)),
            Units = new([]),
            Cities = new([]),
            Annotations = MapAnnotations.Stock with
            {
                Paths = new([new(MapPathKind.TradePartner, new([new(4, 100), new(100, 100)]))])
            }
        };
        var camera = new MapCamera(width / 2 - 50 * 32, height / 2 - 100 * 16, 1);
        var tiles = map.Tiles.ToArray();
        // This gap is far outside the viewport, but invalidates the complete
        // admitted segment whose middle crosses the visible map (P02 MV20/MV29).
        tiles[100 * (map.Width / 2) + 20 / 2] = default;
        var changed = map with { Revision = map.Revision + 1, Tiles = new(tiles) };
        using var retained = new MapPainter(atlas, fonts);
        using var fresh = new MapPainter(atlas, fonts);
        using var actual = SKSurface.Create(new SKImageInfo(width, height));
        using var expected = SKSurface.Create(new SKImageInfo(width, height));
        retained.Draw(actual.Canvas, retained.SetScene(new(map)), camera, width, height, 1, null);
        string visibleRoute = Pixels(actual);
        retained.Draw(actual.Canvas, retained.SetScene(new(changed)), camera, width, height, 1, null);
        fresh.Draw(expected.Canvas, fresh.SetScene(new(changed)), camera, width, height, 1, null);
        Require(Pixels(actual) == Pixels(expected) && Pixels(actual) != visibleRoute,
            "offscreen knowledge loss removes a route crossing the viewport");
    }
}
