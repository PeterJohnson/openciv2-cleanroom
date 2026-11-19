using System.Diagnostics;
using System.Text.Json;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void OverviewFocusRasterChecks(AssetAtlas atlas, PinnedFonts fonts)
    {
        const int width = 640, height = 400;
        var board = MapGallery.Open("MV18");
        var scene = new MapScene(board.Map);
        var point = scene.Locate(scene.Annotations.ActiveUnit!.Value)!.Value;
        var picked = new MapPick(point, MapPickKind.Unit);
        var display = board.Display with { ReducedMotion = true };
        var camera = new MapCamera(27.125, 42.125, .25);
        foreach (int density in new[] { 1, 2 })
        {
            bool reject = false;
            int denied = 0;
            using var painter = new MapPainter(atlas, fonts, allocateRaster: info =>
            {
                if (!reject) return SKSurface.Create(info);
                denied++;
                return null;
            });
            long bound = painter.SetScene(scene);
            using var surface = SKSurface.Create(new SKImageInfo(width * density, height * density));
            surface.Canvas.Scale(density);
            var first = Draw();
            string expected = Pixels(surface);
            reject = true;
            var fallback = Draw();
            Require(first.Error is null && fallback.Error is null && denied == 1 && Pixels(surface) == expected,
                "overview focus preserves the CPU stroke and the overlapping selection and inspection when its patch allocation fails");

            MapDrawStats Draw() => painter.Draw(surface.Canvas, bound, camera, width, height, density,
                picked, display, inspected: picked);
        }
    }

    private static void StartupGroundPreparationChecks(AssetAtlas atlas, PinnedFonts fonts, MapArtSelection native)
    {
        const int width = 960, height = 600;
        var board = MapGallery.Open("MV01");
        var scene = new MapScene(board.Map);
        var camera = BoardCamera(board.Map);
        using var candidate = new MapPainter(atlas, fonts, native);
        using var reference = new MapPainter(atlas, fonts, native, parallelForeground: false, packGroundTextures: false);
        using var actual = SKSurface.Create(new SKImageInfo(width, height));
        using var expected = SKSurface.Create(new SKImageInfo(width, height));
        candidate.BeginStartupGroundPreparation();
        var bound = candidate.SetScene(scene);
        var fresh = reference.SetScene(scene);
        Require(Paint(candidate, actual, bound).Error is null && Paint(reference, expected, fresh).Error is null &&
            Pixels(actual) == Pixels(expected), "prepared ground preserves the selected art on the first draw");
        if (Environment.ProcessorCount > 1)
            Require(candidate.StartupGroundPreparation is { Textures: > 0, TextureBytes: > 0 and <= 8 * 1024 * 1024 },
                "startup ground is admitted within the preparation budget");
        candidate.RecoverResources();
        Require(candidate.TextureUsage == (0, 0) && candidate.GroundUsage == (0, 0) && candidate.ViewportBytes == 0,
            "texture recovery releases the startup ground and frame");

        using var discarded = new MapPainter(atlas, fonts, native);
        discarded.BeginStartupGroundPreparation();
        var former = discarded.SetScene(scene);
        bound = discarded.SelectArt(null);
        Require(discarded.TextureUsage == (0, 0) && Paint(discarded, actual, former).Tiles == 0,
            "replacing art before the first draw discards pending textures and rejects the old frame");
        fresh = reference.SelectArt(null);
        Require(Paint(discarded, actual, bound).Error is null && Paint(reference, expected, fresh).Error is null &&
            discarded.StartupGroundPreparation is null && Pixels(actual) == Pixels(expected),
            "the first stock draw has no prepared textures from the supplanted art");

        MapDrawStats Paint(MapPainter painter, SKSurface surface, long epoch) =>
            painter.Draw(surface.Canvas, epoch, camera, width, height, 1, null, board.Display);
    }

    private static void LiveArtReplacementChecks(AssetAtlas atlas, PinnedFonts fonts, MapArtSelection native,
        MapArtSelection classic, string output)
    {
        const int width = 420, height = 252;
        var camera = new MapCamera(55, 39, .875);
        var board = MapGallery.Open("MV01").Map;
        var unit = new ObservedMapUnit(new(4, 4), new(32), new(1), new(new(3), new(54)), MapUnitDetails.Withheld);
        var city = MapGallery.Open("MV15").Map.Cities[47] with { Position = new(6, 4) };
        var map = board with
        {
            Width = 14,
            Height = 10,
            Flat = true,
            Units = new([unit]),
            Cities = new([city]),
            Tiles = new(Enumerable.Repeat(new ObservedTile(2, default, false, 0, false), 70)),
            Annotations = MapAnnotations.Stock
        };
        var scene = new MapScene(map);
        var pick = new MapPick(unit.Position, MapPickKind.Unit);
        var display = new MapDisplay(Grid: true, Ownership: true, CityRadius: true, ReducedMotion: true);
        var visual = new VisualFrame(new([]), new([]), new(["Coastal survey"]), true);
        var variants = new[] { ("stock", (MapArtSelection?)null), ("native", native), ("classic", classic) };
        using var retained = new MapPainter(atlas, fonts);
        long previous = retained.SetScene(scene);
        using var first = SKSurface.Create(new SKImageInfo(width, height));
        using var doubled = SKSurface.Create(new SKImageInfo(width * 2, height * 2));
        using var overview = SKSurface.Create(new SKImageInfo(204, 118));
        using var blank = SKSurface.Create(new SKImageInfo(width, height));
        doubled.Canvas.Scale(2);
        blank.Canvas.Clear(new SKColor(0xff172b33));
        string blankHash = Pixels(blank);
        string text = MapInspection.Describe(scene, pick, unit.Target);
        Require(scene.Pick(camera, camera.ScreenX(unit.Position.X), camera.ScreenY(unit.Position.Y)) == pick,
            "replacement exercises a selected, visible unit with stable target identity");
        retained.Draw(first.Canvas, previous, camera, width, height, 1, pick);
        Require(retained.TextureUsage.Count > 0 && retained.GroundUsage.Count > 0 && retained.ViewportBytes > 0,
            "warm art replacement begins with all caches populated");
        var samples = new List<object>();
        var referenceImages = new Dictionary<(string Profile, int Density), (string Map, string Overview)>();
        var budgets = new Dictionary<(string Profile, int Density), (long Textures, long Ground, long Viewport)>();
        for (int iteration = 0; iteration < 114; iteration++)
        {
            // The preceding theme is always different, including the first
            // selection out of the warmed stock frame.
            var (profile, art) = variants[(iteration + 1) % variants.Length];
            int density = (iteration / 3) % 2 + 1;
            var surface = density == 1 ? first : doubled;
            long started = Stopwatch.GetTimestamp();
            long epoch = retained.SelectArt(art);
            Require(epoch > previous && retained.GroundUsage == (0, 0) && retained.TextureUsage == (0, 0) &&
                retained.ViewportBytes == 0, "theme replacement frees all rasters before new art is drawn");
            Require(retained.Draw(first.Canvas, previous, camera, width, height, 1, pick).Tiles == 0 && Pixels(first) == blankHash,
                "an already queued old-theme map draw cannot sample the new or old theme");
            retained.DrawOverview(overview.Canvas, previous, 204, 118, camera, width, height, pick.Position);
            using (var pixels = overview.PeekPixels())
                Require(pixels.GetPixelColor(102, 60) == new SKColor(0xff192a2b), "stale overview clears its image");
            var draw = retained.Draw(surface.Canvas, epoch, camera, width, height, density, pick, display, visual);
            double milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Require(draw.Redraw == MapRedraw.Full && draw.Error is null && draw.PreparedChunks > 0 && draw.Notices == 1,
                "replacement presents a fresh map with the permitted active notice");
            retained.DrawOverview(overview.Canvas, epoch, 204, 118, camera, width, height, pick.Position);
            var actual = (Map: Pixels(surface), Overview: Pixels(overview));
            var key = (profile, density);
            if (!referenceImages.TryGetValue(key, out var expected))
            {
                using var independent = new MapPainter(atlas, fonts, art);
                using var reference = SKSurface.Create(new SKImageInfo(width * density, height * density));
                using var inset = SKSurface.Create(new SKImageInfo(204, 118));
                reference.Canvas.Scale(density);
                long fresh = independent.SetScene(new(map));
                independent.Draw(reference.Canvas, fresh, camera, width, height, density, pick, display, visual);
                independent.DrawOverview(inset.Canvas, fresh, 204, 118, camera, width, height, pick.Position);
                expected = (Pixels(reference), Pixels(inset));
                referenceImages.Add(key, expected);
                Encode(surface, Path.Combine(output, $"MV29-theme-{profile}-{density}x.png"));
            }
            Require(actual == expected && MapInspection.Describe(scene, pick, unit.Target) == text,
                "replacement drawing and overview agree with a fresh renderer and keep the same inspection facts");
            var bytes = (retained.TextureUsage.PixelBytes, retained.GroundUsage.PixelBytes, retained.ViewportBytes);
            if (!budgets.TryAdd(key, bytes))
                Require(bytes == budgets[key], "replacing themes does not grow retained raster bytes");
            Require(retained.SelectArt(art) == epoch && retained.Draw(surface.Canvas, epoch, camera, width, height, density, pick, display, visual).Redraw == MapRedraw.Cached &&
                Pixels(surface) == actual.Map, "same admitted theme reuses the warm frame");
            samples.Add(new
            {
                iteration,
                profile,
                density,
                epoch,
                milliseconds,
                actual.Map,
                actual.Overview,
                textures = bytes.Item1,
                ground = bytes.Item2,
                viewport = bytes.Item3
            });
            previous = epoch;
        }
        Require(referenceImages.Count == 6 && referenceImages.Values.Select(image => image.Map).Distinct().Count() == 6,
            "the art set contains three pixel-distinct themes at each backing density");
        File.WriteAllText(Path.Combine(output, "MV29-live-themes.json"), JsonSerializer.Serialize(new
        {
            width,
            height,
            camera,
            map.Identity,
            inspected = text,
            samples
        }, Json));
    }

    private static void DetailedRecoveryChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        const int width = 680, height = 325;
        var camera = new MapCamera(41, 52, 1.1);
        var board = MapGallery.Open("MV21").Map;
        var pick = new MapPick(board.Units[0].Position, MapPickKind.Unit);
        var position = board.Cities[0].Position;
        var effect = new MapEffect(1, MapEffectKind.Explosion, position, position, 300);
        var visual = new VisualFrame(new([new EffectFrame(effect, .43, position.X)]), new([]), new(["Recovery check"]), true);
        var scene = new MapScene(board);
        var results = new List<object>();
        foreach (int density in new[] { 1, 2 })
        {
            using var actual = SKSurface.Create(new SKImageInfo(width * density, height * density));
            using var expected = SKSurface.Create(new SKImageInfo(width * density, height * density));
            actual.Canvas.Scale(density);
            expected.Canvas.Scale(density);
            int fault = 1, requests = 0;
            using var retained = new MapPainter(atlas, fonts);
            using var candidate = new MapPainter(atlas, fonts, allocateRaster: info =>
            {
                requests++;
                return fault == 2 || fault == 1 && info.Width == width * density && info.Height == height * density ? null : SKSurface.Create(info);
            });
            long epoch = candidate.SetScene(scene);
            long fresh = retained.SetScene(new(board));
            var displayDigests = new HashSet<string>(StringComparer.Ordinal);
            foreach (var color in Enum.GetValues<MapColorCorrection>())
            {
                var display = new MapDisplay(Color: color, Ownership: true, CityRadius: true, Grid: true);
                retained.InvalidateFrame();
                var reference = retained.Draw(expected.Canvas, fresh, camera, width, height, density, pick, display, visual);
                string correct = Pixels(expected);
                Require(displayDigests.Add(correct), "all display correction modes produce different pixels");
                candidate.RecoverResources();
                fault = 1;
                var direct = candidate.Draw(actual.Canvas, epoch, camera, width, height, density, pick, display, visual);
                Require(direct.Redraw == MapRedraw.Direct && direct.Objects == reference.Objects && direct.Labels > 0 && direct.Labels == reference.Labels &&
                    direct.Effects == 1 && direct.Notices == 1 && Pixels(actual) == correct, "CPU direct drawing preserves all visible layers");
                candidate.RecoverResources();
                fault = 2;
                var failed = candidate.Draw(actual.Canvas, epoch, camera, width, height, density, pick, display, visual);
                Require(failed is { Redraw: MapRedraw.Error, Objects: 0, Effects: 0, Notices: 0 } && failed.Error is not null &&
                    candidate.TextureUsage == (0, 0) && candidate.GroundUsage == (0, 0) && candidate.ViewportBytes == 0,
                    "failure leaves a limited frame and releases the previously displayed map");
                int failedRequests = requests;
                fault = 0;
                var held = candidate.Draw(actual.Canvas, epoch, camera, width, height, density, pick, display, visual);
                Require(held.Error == failed.Error && requests == failedRequests, "limited frame avoids repeated allocation");
                candidate.RecoverResources();
                var restored = candidate.Draw(actual.Canvas, epoch, camera, width, height, density, pick, display, visual);
                Require(restored.Redraw == MapRedraw.Full && restored.Error is null && Pixels(actual) == correct &&
                    scene.Pick(camera, camera.ScreenX(pick.Position.X), camera.ScreenY(pick.Position.Y)) == pick,
                    "recovery preserves the selected target and exact visible frame");
                results.Add(new
                {
                    density,
                    color = color.ToString(),
                    direct.Objects,
                    direct.Labels,
                    direct.Effects,
                    direct.Notices,
                    failed.Error,
                    digest = correct
                });
            }
        }
        File.WriteAllText(Path.Combine(output, "MV30-full-recovery.json"), JsonSerializer.Serialize(results, Json));
    }
}
