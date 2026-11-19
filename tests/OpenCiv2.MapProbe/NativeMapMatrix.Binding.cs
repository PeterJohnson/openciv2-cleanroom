using OpenCiv2.App.Map;
using OpenCiv2.Formats.Graphics;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void FirstFrameBodyPreparationChecks(AssetAtlas atlas, PinnedFonts fonts)
    {
        const int width = 1920, height = 1080, density = 2;
        var board = MapGallery.Open("Mixed");
        var scene = new MapScene(board.Map);
        var camera = new MapCamera(410, -200, .35714285714285715);
        using var painter = new MapPainter(atlas, fonts);
        long bound = painter.SetScene(scene);
        if (Environment.ProcessorCount < 8)
        {
            Require(!Queue(bound), "small worker pools skip initial layout preparation");
            return;
        }

        using var reference = new MapPainter(atlas, fonts, parallelForeground: false, packGroundTextures: false);
        long fresh = reference.SetScene(scene);
        var info = new SKImageInfo(width * density, height * density);
        using var actual = SKSurface.Create(info);
        using var expected = SKSurface.Create(info);
        actual.Canvas.Scale(density);
        expected.Canvas.Scale(density);
        Require(Queue(bound), "first layout queues visible bodies for a large frame");
        var draw = Paint(painter, actual, bound);
        Paint(reference, expected, fresh);
        Require(draw.Error is null && draw.EarlyBodyLeadMs > 0 && draw.OverlappedBodyTextures > 0 && Pixels(actual) == Pixels(expected),
            "the matching first draw admits the prepared bodies and matches a fresh renderer");

        painter.RecoverResources();
        Require(Queue(bound), "a cold replacement can schedule body preparation");
        camera = camera with { X = camera.X + 44 };
        draw = Paint(painter, actual, bound);
        Paint(reference, expected, fresh);
        Require(draw.Error is null && draw.EarlyBodyLeadMs == 0 && Pixels(actual) == Pixels(expected),
            "the first draw after a camera change discards preparation for the previous view");

        painter.RecoverResources();
        Require(Queue(bound), "initial layout may overlap a world revision");
        var revised = new MapScene(MapGallery.Changed(board.Map));
        bound = painter.SetScene(revised);
        fresh = reference.SetScene(revised);
        draw = Paint(painter, actual, bound);
        Paint(reference, expected, fresh);
        Require(draw.Error is null && draw.EarlyBodyLeadMs == 0 && Pixels(actual) == Pixels(expected),
            "a revision replaces prepared bodies even when the frame geometry stays the same");

        painter.RecoverResources();
        Require(Queue(bound), "old observer preparation starts before the observer swap");
        var replacement = new MapScene(board.Map with
        {
            Identity = board.Map.Identity with { Observer = new(2) },
            Units = new([]),
            Cities = new([])
        });
        long current = painter.SetScene(replacement);
        Require(painter.TextureUsage == (0, 0) && Paint(painter, actual, bound).Tiles == 0,
            "observer replacement discards queued body work and rejects the old frame");
        fresh = reference.SetScene(replacement);
        draw = Paint(painter, actual, current);
        var serial = Paint(reference, expected, fresh);
        Require(draw.Error is null && draw.EarlyBodyLeadMs == 0 && draw.Objects == serial.Objects &&
            draw.OverlappedBodyTextures == 0 && Pixels(actual) == Pixels(expected),
            "the replacement view draws only its admitted bodies and ground");

        bool Queue(long epoch) =>
            painter.TryPrepareFirstFrameBodies(epoch, camera, width, height, density, board.Display, null);

        MapDrawStats Paint(MapPainter renderer, SKSurface surface, long epoch) =>
            renderer.Draw(surface.Canvas, epoch, camera, width, height, density, null, board.Display);
    }

    private static void HigherBodyPreparationChecks(AssetAtlas atlas, PinnedFonts fonts)
    {
        if (Environment.ProcessorCount < 2) return;
        const int width = 1920, height = 1080, density = 2;
        var board = MapGallery.Open("Mixed");
        var scene = new MapScene(board.Map);
        var first = new MapCamera(410, -200, .7);
        var nearer = first.ZoomAt(width / 2, height / 2, .75);
        var high = nearer.ZoomAt(width / 2, height / 2, 1.25);
        using var candidate = new MapPainter(atlas, fonts);
        using var serial = new MapPainter(atlas, fonts, parallelForeground: false, packGroundTextures: false);
        var info = new SKImageInfo(width * density, height * density);
        using var actual = SKSurface.Create(info);
        using var expected = SKSurface.Create(info);
        actual.Canvas.Scale(density);
        expected.Canvas.Scale(density);
        long epoch = candidate.SetScene(scene);
        Require(Paint(candidate, actual, epoch, first).HigherBodyTextures == 0, "cold draw leaves high-tier look-ahead idle");
        Paint(candidate, actual, epoch, nearer);
        MapDrawStats published = default;
        bool ready = SpinWait.SpinUntil(() =>
        {
            published = Paint(candidate, actual, epoch, nearer);
            return published.HigherBodyTextures > 0 || published.Error is not null;
        }, TimeSpan.FromSeconds(15));
        Require(ready && published.Error is null && published.HigherBodyTextures is >= 1 and <= 4 && published.HigherBodyWorkMs > 0,
            "inward zoom publishes one capped batch of prepared bodies");
        var crossed = Paint(candidate, actual, epoch, high);
        Paint(serial, expected, serial.SetScene(scene), high);
        Require(crossed.Error is null && Pixels(actual) == Pixels(expected),
            "prepared higher-tier bodies match fresh pixels through the tier crossing");

        candidate.RecoverResources();
        Paint(candidate, actual, epoch, first);
        Paint(candidate, actual, epoch, nearer);
        var replacement = new MapScene(board.Map with
        {
            Identity = board.Map.Identity with { Observer = new(2) },
            Units = new([]),
            Cities = new([])
        });
        long rebound = candidate.SetScene(replacement);
        Require(Paint(candidate, actual, epoch, nearer).Tiles == 0, "observer swap rejects the queued draw");
        var settled = Paint(candidate, actual, rebound, high);
        serial.SetScene(null);
        Paint(serial, expected, serial.SetScene(replacement), high);
        Require(settled.Error is null && settled.HigherBodyTextures == 0 && Pixels(actual) == Pixels(expected),
            "observer swap discards the pending higher-tier batch and presents fresh scoped pixels");

        MapDrawStats Paint(MapPainter painter, SKSurface surface, long binding, MapCamera camera) =>
            painter.Draw(surface.Canvas, binding, camera, width, height, density, null, board.Display);
    }

    private static void ConcurrentSceneBindingChecks(AssetAtlas atlas, PinnedFonts fonts)
    {
        const int width = 360, height = 240;
        var timeout = TimeSpan.FromSeconds(15);
        var info = new SKImageInfo(width, height);
        var map = MapGallery.Open("MV26").Map;
        var oldScene = new MapScene(map);
        var intermediate = new MapScene(MapGallery.Changed(map));
        var latestMap = MapGallery.Changed(intermediate.Map);
        var tiles = latestMap.Tiles.ToArray();
        tiles[latestMap.Width + 2] = default; // W01 (4, 2) loses disclosure.
        var latest = new MapScene(latestMap with { Tiles = new(tiles) });
        var camera = new MapCamera(48, 60, 1);
        var display = new MapDisplay(Color: MapColorCorrection.HighContrast);
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        bool pause = true;
        using var painter = new MapPainter(atlas, fonts, allocateRaster: backing =>
        {
            if (pause)
            {
                pause = false;
                entered.Set();
                if (!resume.Wait(timeout)) throw new TimeoutException("Test did not resume map rasterization");
            }
            return SKSurface.Create(backing);
        });
        using var target = SKSurface.Create(info);
        using var expected = SKSurface.Create(info);
        using var reference = new MapPainter(atlas, fonts, parallelForeground: false, packGroundTextures: false);
        long original = painter.SetScene(oldScene);
        var inFlight = Task.Run(() => painter.Draw(target.Canvas, original, camera, width, height, 1, null, display));
        long newest = 0;
        try
        {
            Require(entered.Wait(timeout), "the old draw holds its raster resources before publication");
            var update = Task.Run(() =>
            {
                long skipped = painter.SetScene(intermediate, measure: true, out var first);
                long published = painter.SetScene(latest, measure: true, out var second);
                return (skipped, published, first.Deferred, SecondDeferred: second.Deferred);
            });
            Require(update.Wait(timeout), "same-layout publication completes while the old raster is busy");
            var receipt = update.Result;
            newest = receipt.published;
            Require(receipt.Deferred && receipt.SecondDeferred && original < receipt.skipped && receipt.skipped < newest,
                "concurrent updates publish distinct generations in order");
        }
        finally
        {
            resume.Set();
            Require(inFlight.Wait(timeout), "rasterization exits after the replacement");
        }
        expected.Canvas.Clear(new SKColor(0xff172b33));
        Require(inFlight.Result.Tiles == 0 && Pixels(target) == Pixels(expected),
            "an in-flight stale generation publishes no map pixels, including with display correction");

        painter.DrawOverview(target.Canvas, original, width, height, camera, width, height, null);
        expected.Canvas.Clear(new SKColor(0xff192a2b));
        Require(Pixels(target) == Pixels(expected), "a stale overview publishes only the background");
        long referenceEpoch = reference.SetScene(latest);
        painter.DrawOverview(target.Canvas, newest, width, height, camera, width, height, null);
        reference.DrawOverview(expected.Canvas, referenceEpoch, width, height, camera, width, height, null);
        Require(Pixels(target) == Pixels(expected), "an overview can consume the latest queued generation before the map");
        painter.Draw(target.Canvas, newest, camera, width, height, 1, null, display);
        reference.Draw(expected.Canvas, referenceEpoch, camera, width, height, 1, null, display);
        Require(Pixels(target) == Pixels(expected), "coalesced revisions repaint terrain and objects exactly");

        painter.RecoverResources();
        entered.Reset();
        resume.Reset();
        pause = true;
        var former = Task.Run(() => painter.Draw(target.Canvas, newest, camera, width, height, 1, null));
        var observer = new MapScene(latest.Map with { Identity = latest.Map.Identity with { Observer = new(2) } });
        using var replacing = new ManualResetEventSlim();
        Task<(long Epoch, MapPainter.BindingTiming Timing)> replacement;
        try
        {
            Require(entered.Wait(timeout), "observer replacement overlaps an in-flight draw");
            replacement = Task.Run(() =>
            {
                replacing.Set();
                long epoch = painter.SetScene(observer, measure: true, out var timing);
                return (epoch, timing);
            });
            Require(replacing.Wait(timeout) && !replacement.IsCompleted, "observer replacement waits for the old raster writer");
        }
        finally { resume.Set(); }
        Require(former.Wait(timeout) && replacement.Wait(timeout) && !replacement.Result.Timing.Deferred &&
            replacement.Result.Epoch > newest && painter.ViewportBytes == 0 && painter.GroundUsage.Count == 0,
            "returning from observer replacement releases the previous observer's rasters");
        painter.Draw(target.Canvas, newest, camera, width, height, 1, null);
        expected.Canvas.Clear(new SKColor(0xff172b33));
        Require(Pixels(target) == Pixels(expected), "an old observer cannot redraw after replacement");
    }
}
