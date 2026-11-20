using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void ScopedRouteMemoryChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        using var session = new SeamSession();
        var world = session.Runtime.World;
        var riverbend = new MapScene(session.Player.ObserveMap()!);
        var sprites = new List<TileSprite>();
        CheckRiverbendArt(new(12, 4), "river/5", "road/2", "road/6");
        CheckRiverbendArt(new(9, 7), "river/13");
        CheckRiverbendArt(new(55, 11), "mouth/0");
        CheckRiverbendArt(new(14, 6), "resource/6");

        var point = new MapPoint(14, 10);
        var east = new MapPoint(16, 10);
        for (int y = 8; y <= 12; y++)
            for (int x = 10 + (y & 1); x <= 20; x += 2)
                world.Map.Remembered(1, new(x, y)) = 0;
        world.Map.Remembered(1, new(point.X, point.Y)) = MapFeatures.Road;
        world.Map.Remembered(1, new(east.X, east.Y)) = MapFeatures.Railroad;
        session.Runtime.Publish();
        var mixed = session.Player.ObserveMap()!;
        Require(mixed.Identity.Observer.Value == 1 && new MapScene(mixed).Tile(east).Terrain is not null,
            "the observer has explored the test road and rail tiles");
        var random = session.Runtime.GameplayRandom.Capture();
        var cosmetic = session.Runtime.PresentationRandom.Capture();
        world.Map.TileAt(new(east.X, east.Y)).Features ^= MapFeatures.Railroad | MapFeatures.Road;
        session.Runtime.Publish();
        var hiddenChange = session.Player.ObserveMap()!;
        Require(hiddenChange.Revision != mixed.Revision && hiddenChange.Tiles.Equals(mixed.Tiles),
            "changing live route flags without a memory publication does not update W03 tiles");
        world.Map.Remembered(1, new(point.X, point.Y)) = MapFeatures.Railroad;
        session.Runtime.Publish();
        var rails = session.Player.ObserveMap()!;
        world.Map.Remembered(1, new(east.X, east.Y)) = 0;
        session.Runtime.Publish();
        var isolated = session.Player.ObserveMap()!;

        const int width = 640, height = 360;
        using var retained = new MapPainter(atlas, fonts);
        using var fresh = new MapPainter(atlas, fonts);
        var samples = new List<object>();
        foreach (var (zoom, density) in new[] { (1d, 1), (.5, 2) })
        {
            using var target = SKSurface.Create(new SKImageInfo(width * density, height * density));
            using var comparison = SKSurface.Create(new SKImageInfo(width * density, height * density));
            target.Canvas.Scale(density);
            comparison.Canvas.Scale(density);
            var camera = new MapCamera(width / 2 - 14 * 32 * zoom, height / 2 - 10 * 16 * zoom, zoom);
            retained.SetScene(null);
            string old = Check("mixed", mixed, "road/2");
            Require(Check("unpublished", hiddenChange, "road/2") == old,
                "unpublished route changes preserve every pixel");
            string upgraded = Check("published-rail", rails, "rail/2");
            string ended = Check("published-isolated", isolated, "rail/0");
            Require(old != upgraded && upgraded != ended && ended != old, "a published route memory change redraws the adjacent junction");

            string Check(string name, MapObservation value, string binding)
            {
                var scene = new MapScene(value);
                var selected = new List<TileSprite>();
                MapTileArt.Select(selected, scene, point, MapTileArt.Detail(zoom), false);
                Require(selected.Where(sprite => sprite.Layer is >= TileArtLayer.RouteNE and <= TileArtLayer.RouteN)
                    .Select(sprite => sprite.Key).SequenceEqual([binding]), $"planned endpoint {name} uses only {binding}");
                long epoch = retained.SetScene(scene);
                var result = retained.Draw(target.Canvas, epoch, camera, width, height, density, null);
                fresh.SetScene(null);
                fresh.Draw(comparison.Canvas, fresh.SetScene(new(value)), camera, width, height, density, null);
                string hash = Pixels(target);
                Require(hash == Pixels(comparison), $"{name} matches a fresh scoped route image");
                if (name.StartsWith("published-", StringComparison.Ordinal))
                    Require(result.Redraw == MapRedraw.Dirty, $"{name} reaches the dirty-world redraw");
                string image = $"MV07-memory-{name}-{density}x.png";
                Encode(target, Path.Combine(output, image));
                samples.Add(new
                {
                    name,
                    value.Identity,
                    value.Revision,
                    zoom,
                    density,
                    width,
                    height,
                    camera,
                    binding,
                    redraw = result.Redraw.ToString(),
                    image,
                    imageSha256 = FileHash(Path.Combine(output, image))
                });
                return hash;
            }
        }
        Require(random == session.Runtime.GameplayRandom.Capture() && cosmetic == session.Runtime.PresentationRandom.Capture() &&
            session.Player.ObserveMap()?.Revision == isolated.Revision, "route reconstruction preserves both RNG streams and the scoped revision");
        File.WriteAllText(Path.Combine(output, "MV07-route-memory.json"), JsonSerializer.Serialize(samples, Json));

        void CheckRiverbendArt(MapPoint point, params string[] keys)
        {
            MapTileArt.Select(sprites, riverbend, point, MapDetail.Full, false);
            foreach (string key in keys)
                Require(sprites.Exists(sprite => sprite.Key == key), $"Riverbend {point} selects the pinned {key}");
        }
    }
}
