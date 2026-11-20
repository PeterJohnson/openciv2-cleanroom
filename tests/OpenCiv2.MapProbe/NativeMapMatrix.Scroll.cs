using System.Text.Json;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void SparseGroundDamageChecks(AssetAtlas atlas, PinnedFonts fonts)
    {
        const int width = 1920, height = 1080, density = 2;
        var camera = new MapCamera(-100, -180, 1);
        var map = MapGallery.Open("Mixed").Map;
        var tiles = map.Tiles.ToArray();
        // Each change lies at a chunk center. Resident chunks between the
        // changes lie inside the same backing-pixel damage rectangle.
        foreach (var (x, y) in new[] { (11, 23), (35, 55) })
        {
            int index = y * (map.Width / 2) + x / 2;
            tiles[index] = tiles[index] with { Terrain = tiles[index].Terrain == 2 ? (byte)4 : (byte)2 };
        }
        var revised = map with { Revision = map.Revision + 1, Tiles = new(tiles) };
        var info = new SKImageInfo(width * density, height * density);
        bool denyScratch = false;
        using var candidate = new MapPainter(atlas, fonts);
        using var limited = new MapPainter(atlas, fonts, allocateRaster: size =>
            denyScratch && size.Width == info.Width && size.Height == info.Height ? null : SKSurface.Create(size));
        using var reference = new MapPainter(atlas, fonts, parallelForeground: false, packGroundTextures: false);
        using var target = SKSurface.Create(info);
        using var expected = SKSurface.Create(info);
        target.Canvas.Scale(density);
        expected.Canvas.Scale(density);
        var initial = Paint(candidate, target, map);
        var dirty = Paint(candidate, target, revised);
        var fresh = Paint(reference, expected, revised);
        bool hasWorkers = Environment.ProcessorCount > 1;
        Require(initial.Error is null && fresh.Error is null && dirty.Redraw == MapRedraw.Dirty &&
            dirty.PreparedChunks == 2 && dirty.GroundChunks > dirty.PreparedChunks &&
            dirty.ParallelGroundChunks == (hasWorkers ? 2 : 0) && (dirty.ForegroundBands > 0) == hasWorkers &&
            Pixels(target) == Pixels(expected), "sparse dirty chunks batch across cache hits and replay exactly inside a large damage patch");
        Paint(limited, target, map);
        denyScratch = true;
        var fallback = Paint(limited, target, revised);
        Require(fallback.Redraw == MapRedraw.Dirty && fallback.ForegroundBands == 0 && Pixels(target) == Pixels(expected),
            "denying dirty foreground scratch preserves map pixels and the retained viewport");

        MapDrawStats Paint(MapPainter painter, SKSurface surface, MapObservation observation) =>
            painter.Draw(surface.Canvas, painter.SetScene(new(observation)), camera, width, height, density, null);
    }

    private static void SameTierBodyPreparationChecks(AssetAtlas atlas, PinnedFonts fonts)
    {
        const int width = 320, height = 200, density = 2;
        var map = MapGallery.Open("Mixed").Map;
        var units = map.Units.DistinctBy(unit => unit.Type).Take(4).ToArray();
        units[0] = units[0] with { Position = new(4, 4) };
        units[1] = units[1] with { Position = new(6, 4) };
        units[2] = units[2] with { Position = new(64, 4) };
        units[3] = units[3] with { Position = new(66, 4) };
        var scene = new MapScene(map with { Units = new(units), Cities = new([]) });
        var first = new MapCamera(20, 20, 1.1);
        var second = first.Pan(-60 * MapCamera.HalfWidth * first.Zoom, 0, map, width, height);
        using var painter = new MapPainter(atlas, fonts);
        using var fresh = new MapPainter(atlas, fonts, parallelForeground: false, packGroundTextures: false);
        using var target = SKSurface.Create(new SKImageInfo(width * density, height * density));
        using var expected = SKSurface.Create(new SKImageInfo(width * density, height * density));
        target.Canvas.Scale(density);
        expected.Canvas.Scale(density);
        long epoch = painter.SetScene(scene);
        var initial = painter.Draw(target.Canvas, epoch, first, width, height, density, null);
        var changed = painter.Draw(target.Canvas, epoch, second, width, height, density, null);
        var reference = fresh.Draw(expected.Canvas, fresh.SetScene(scene), second, width, height, density, null);
        int parallel = Environment.ProcessorCount > 1 ? 2 : 0;
        Require(initial.Error is null && changed.Error is null && reference.Error is null &&
            initial.PreparedBodyTextures == parallel && changed.PreparedBodyTextures == parallel && Pixels(target) == Pixels(expected),
            "a pan in the same texture tier prepares the newly visible unit bodies and matches fresh pixels");
        Require(painter.Draw(target.Canvas, epoch, second, width, height, density, null).PreparedBodyTextures == 0,
            "an unchanged viewport performs no body texture preparation");
    }

    private static void SettledTerrainChecks(AssetAtlas atlas, PinnedFonts fonts)
    {
        if (Environment.ProcessorCount < 2) return;
        const int width = 1920, height = 1080;
        var source = MapGallery.Open("Mixed").Map;
        var safe = new MapCamera(319.7632927206961, -180.26629568921692, .3572749482585401);
        // The preceding box at this X reaches beyond the next box's left
        // edge near device column 45.5. It needs ordered full replay.
        var tie = safe with { X = -57.27959840991299 };
        using var candidate = new MapPainter(atlas, fonts);
        using var reference = new MapPainter(atlas, fonts, parallelForeground: false, packGroundTextures: false);
        foreach (var (camera, flat, retain, density) in new[] { (safe, false, true, 1), (safe, false, true, 2), (safe, true, true, 2), (tie, false, false, 2) })
        {
            long pixels = (long)width * height * density * density;
            var info = new SKImageInfo(width * density, height * density);
            using var actual = SKSurface.Create(info);
            using var expected = SKSurface.Create(info);
            actual.Canvas.Scale(density);
            expected.Canvas.Scale(density);
            var map = source with { Flat = flat };
            Paint(candidate, actual, candidate.SetScene(new(map)), camera);
            var focus = camera.Hit(width * .48, height * .52, map)!.Value;
            map = MapGallery.Changed(map, focus);
            var seeded = Paint(candidate, actual, candidate.SetScene(new(map)), camera);
            Require(seeded.Error is null && seeded.GroundDamagePixels == pixels,
                "the first update establishes or fully renders settled terrain");

            var second = camera.Hit(width * .82, height * .27, map)!.Value;
            map = MapGallery.Changed(map, second);
            candidate.SetScene(new(map));
            // Hide a separate tile in the following revision before the UI
            // draws. The retained raster must consume both invalidations.
            map = MapGallery.Changed(map, focus);
            var tiles = map.Tiles.ToArray();
            tiles[focus.Y * (map.Width / 2) + focus.X / 2] = default;
            map = map with { Tiles = new(tiles) };
            long current = candidate.SetScene(new(map));
            var edited = Paint(candidate, actual, current, camera);
            reference.SetScene(null);
            Paint(reference, expected, reference.SetScene(new(map)), camera);
            Require(edited.Error is null && Pixels(actual) == Pixels(expected),
                "accumulated terrain, road and fog edits match fresh pixels at the settled camera");
            Require(retain ? edited.GroundDamagePixels > 0 && edited.GroundDamagePixels < pixels / 8 : edited.GroundDamagePixels == pixels,
                "the settled update repaints changed chunks or takes the full replay at a tie");

            var replacement = new MapScene(map with { Identity = map.Identity with { Observer = new(2) } });
            long rebound = candidate.SetScene(replacement);
            Require(candidate.GroundUsage == (0, 0) && candidate.ViewportBytes == 0 && Paint(candidate, actual, current, camera).Tiles == 0,
                "observer swap discards settled terrain and rejects the old frame");
            Paint(candidate, actual, rebound, camera);
            Paint(reference, expected, reference.SetScene(replacement), camera);
            Require(Pixels(actual) == Pixels(expected), "the new observer rebuilds the settled view from its observations");

            MapDrawStats Paint(MapPainter renderer, SKSurface surface, long epoch, MapCamera position) =>
                renderer.Draw(surface.Canvas, epoch, position, width, height, density, null);
        }
    }

    private static void LargeForegroundBandSeamChecks(AssetAtlas atlas, PinnedFonts fonts)
    {
        const int width = 2560, height = 1440, density = 2;
        var board = MapGallery.Open("Mixed");
        var scene = new MapScene(board.Map);
        // Recorded adjacent native zoom cameras. In the second frame a fine
        // map feature spans the first 240-pixel worker seam at 5K.
        var before = new MapCamera(783.4486898636262, 87.94058404223836, .30306196193893437);
        var after = new MapCamera(802.3203294671018, 105.28654462723989, .2939045459982249);
        var backings = new List<SKSurface>();
        using var painter = new MapPainter(atlas, fonts, allocateRaster: info =>
        {
            var surface = SKSurface.Create(info);
            if (surface is not null && info.Width == width * density && info.Height == height * density)
                backings.Add(surface);
            return surface;
        });
        using var reference = new MapPainter(atlas, fonts, parallelForeground: false, packGroundTextures: false);
        using var target = SKSurface.Create(new SKImageInfo(width * density, height * density));
        using var expected = SKSurface.Create(new SKImageInfo(width * density, height * density));
        target.Canvas.Scale(density);
        expected.Canvas.Scale(density);
        long epoch = painter.SetScene(scene);
        using var recorder = new SKPictureRecorder();
        var recording = recorder.BeginRecording(SKRect.Create(width * density, height * density));
        recording.Scale(density);
        var first = painter.Draw(recording, epoch, before, width, height, density, null, board.Display);
        using var previous = recorder.EndRecording();
        var drawn = painter.Draw(target.Canvas, epoch, after, width, height, density, null, board.Display);
        reference.Draw(expected.Canvas, reference.SetScene(scene), after, width, height, density, null, board.Display);
        int workers = Math.Min(12, Environment.ProcessorCount);
        int expectedBands = workers > 1 ? workers : 0;
        Require(first.Error is null && first.GroundReplayBands == expectedBands &&
            drawn.Error is null && drawn.GroundReplayBands == expectedBands &&
            backings.Count == (workers > 1 ? 2 : 1) &&
            Pixels(target) == Pixels(expected), "the cold 5K frame and subsequent zoom replay ground, preserving pixels at the seam");
        using (new SKAutoCanvasRestore(target.Canvas, true))
        {
            target.Canvas.ResetMatrix();
            target.Canvas.DrawPicture(previous);
        }
        reference.Draw(expected.Canvas, reference.SetScene(scene), before, width, height, density, null, board.Display);
        Require(Pixels(target) == Pixels(expected), "a submitted 5K map image remains unchanged when the next frame replaces its backing");

        // Start cold at a stable sampling phase. The first pan establishes
        // the terrain backing and the next uses it for the scroll.
        painter.RecoverResources();
        Require(painter.ViewportBytes == 0 && backings.TrueForAll(surface => surface.Handle == IntPtr.Zero),
            "recovery releases the cold frame and its shared scratch");
        backings.Clear();
        var stable = new MapCamera(-120, -50, .5);
        var coldStable = painter.Draw(target.Canvas, epoch, stable, width, height, density, null, board.Display);
        reference.Draw(expected.Canvas, reference.SetScene(scene), stable, width, height, density, null, board.Display);
        Require(coldStable.GroundReplayBands == expectedBands && Pixels(target) == Pixels(expected),
            "cold rendering preserves fresh pixels at a stable sampling phase");
        stable = stable.Pan(.5, .5, board.Map, width, height);
        var firstPan = painter.Draw(target.Canvas, epoch, stable, width, height, density, null, board.Display);
        reference.Draw(expected.Canvas, reference.SetScene(scene), stable, width, height, density, null, board.Display);
        Require((expectedBands == 0 || firstPan.Redraw == MapRedraw.Full) && Pixels(target) == Pixels(expected),
            "the first stable pan establishes the scroll raster and matches fresh pixels");
        stable = stable.Pan(.5, .5, board.Map, width, height);
        var scrolled = painter.Draw(target.Canvas, epoch, stable, width, height, density, null, board.Display);
        reference.Draw(expected.Canvas, reference.SetScene(scene), stable, width, height, density, null, board.Display);
        Require(scrolled.Redraw == MapRedraw.Scrolled && backings.Count == 3 &&
            painter.ViewportBytes == 3L * width * height * density * density * 4 &&
            Pixels(target) == Pixels(expected), "5K terrain scroll and seam repair share one viewport backing");
        painter.RecoverResources();
        Require(painter.ViewportBytes == 0 && backings.TrueForAll(surface => surface.Handle == IntPtr.Zero),
            "texture recovery releases each 5K backing, including shared scratch");
    }

    private static void ForegroundBandFallbackChecks(AssetAtlas atlas, PinnedFonts fonts)
    {
        const int width = 1000, height = 620, density = 2;
        var info = new SKImageInfo(width * density, height * density);
        // This sampling phase uses a direct ground repaint, so only the
        // foreground band path requests a second viewport-sized surface.
        var camera = new MapCamera(1.5 / density, 17.5 / density, 1d / density);
        var scene = new MapScene(MapGallery.Open("Mixed").Map);
        var display = new MapDisplay(Grid: true, Ownership: true, CityRadius: true);
        int viewports = 0;
        using var limited = new MapPainter(atlas, fonts, allocateRaster: size =>
            size.Width == info.Width && size.Height == info.Height && ++viewports > 1 ? null : SKSurface.Create(size));
        using var parallel = new MapPainter(atlas, fonts);
        using var reference = new MapPainter(atlas, fonts, parallelForeground: false);
        using var target = SKSurface.Create(info);
        using var expected = SKSurface.Create(info);
        target.Canvas.Scale(density);
        expected.Canvas.Scale(density);
        var fresh = Paint(reference, expected);
        string expectedPixels = Pixels(expected);
        var banded = Paint(parallel, target);
        var fallback = Paint(limited, target);
        bool hasWorkers = Environment.ProcessorCount > 1;
        Require(fresh.Error is null && banded.Error is null && fallback.Error is null && fallback.ForegroundBands == 0 &&
            fallback.Objects == fresh.Objects && fallback.Labels == fresh.Labels &&
            banded.Objects == fresh.Objects && banded.Labels == fresh.Labels &&
            (banded.ForegroundBands > 0) == hasWorkers && viewports == (hasWorkers ? 2 : 1) &&
            Pixels(target) == expectedPixels, "parallel foreground scratch denial preserves a complete serial frame");
        var cached = Paint(parallel, target);
        Require((banded.PreparedBodyTextures > 0) == hasWorkers &&
            (banded.OverlappedBodyTextures > 0) == (Environment.ProcessorCount >= 8) &&
            cached.PreparedBodyTextures == 0 && cached.OverlappedBodyTextures == 0 &&
            Pixels(target) == expectedPixels, "body preparation preserves serial clipping, alpha and overlay order");
        parallel.RecoverResources();
        var recovered = Paint(parallel, target);
        Require(recovered.PreparedBodyTextures == banded.PreparedBodyTextures &&
            recovered.OverlappedBodyTextures == banded.OverlappedBodyTextures && Pixels(target) == expectedPixels,
            "texture recovery clears body preparation for the current backing tier");

        camera = camera.Pan(1d / density, 1d / density, scene.Map, width, height);
        var directGround = Paint(reference, expected);
        var replayedGround = Paint(parallel, target);
        string pannedPixels = Pixels(target);
        var serialGround = Paint(limited, target);
        Require(directGround.Error is null && replayedGround.Redraw == MapRedraw.Full && serialGround.Redraw == MapRedraw.Full &&
            replayedGround.GroundReplayBands == replayedGround.ForegroundBands && (replayedGround.GroundReplayBands > 0) == hasWorkers &&
            serialGround.GroundReplayBands == 0 && replayedGround.GroundChunks == directGround.GroundChunks &&
            pannedPixels == Pixels(expected) && Pixels(target) == pannedPixels,
            "fractional sampling uses exact ground and foreground replay with a complete serial fallback");
        Require(Paint(parallel, target).GroundReplayBands == 0, "unchanged frames do not replay banded ground");

        MapDrawStats Paint(MapPainter painter, SKSurface surface) =>
            painter.Draw(surface.Canvas, painter.SetScene(scene), camera, width, height, density, null, display);
    }

    private static void ViewportScrollChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        const int width = 832, height = 522, border = 8;
        var stock = MapGallery.Open("MV20").Map;
        var samples = new List<object>();
        using var reference = new MapPainter(atlas, fonts);
        foreach (var flat in new[] { false, true })
            foreach (int density in new[] { 1, 2 })
            {
                double zoom = flat ? 1 : .75;
                double period = stock.Width * MapCamera.HalfWidth * zoom;
                var pixels = new SKImageInfo(width * density + 2 * border, height * density + 2 * border);
                using var candidate = new MapPainter(atlas, fonts);
                using var target = SKSurface.Create(pixels);
                using var expected = SKSurface.Create(pixels);
                var world = stock with { Flat = flat };
                var camera = new MapCamera(flat ? 32 * zoom - period + 2d / density : period - 1.875 / density, 17 + .125 / density, zoom);
                long epoch = candidate.SetScene(new(world));
                long fresh = reference.SetScene(new(world));
                var effectPoint = flat ? new MapPoint(world.Width - 4, 6) : new MapPoint(4, 6);
                var effect = new MapEffect(1, MapEffectKind.Explosion, effectPoint, effectPoint, 300);
                var display = new MapDisplay(Grid: true, Ownership: true, CityRadius: true);
                var pick = new MapPick(effectPoint, MapPickKind.Tile);
                Capture(MapRedraw.Full, 0);
                camera = camera.Pan((flat ? -1d : 1d) / density, 0, world, width, height);
                Capture(MapRedraw.Scrolled, 1);
                camera = camera.Pan((flat ? -4d : 4d) / density, 2d / density, world, width, height);
                Require(flat ? camera.X == 32 * zoom - period : camera.X < 10, "pan reaches the flat edge or crosses the wrapped seam");
                Capture(MapRedraw.Scrolled, 2);
                Encode(target, Path.Combine(output, $"MV29-{(flat ? "flat" : "wrapped")}-scroll-{density}x.png"));
                camera = camera.Pan(5d / density, 3d / density, world, width, height);
                Capture(MapRedraw.Scrolled, 3);
                camera = camera.Pan(.25 / density, 0, world, width, height);
                Capture(MapRedraw.Full, 4);
                Require(candidate.ViewportBytes <= 3L * width * height * density * density * 4, "retained viewport uses at most three rasters");
                camera = new MapCamera(1.5 / density, 17.5 / density, 1 / (double)density);
                Capture(MapRedraw.Full, 5);
                camera = camera.Pan(1d / density, 1d / density, world, width, height);
                Capture(MapRedraw.Full, 6);
                Require(candidate.ViewportBytes == (long)width * height * density * density * 4,
                    "sampling-boundary fallback releases optional terrain buffers");
                candidate.SetScene(new(world with { Identity = world.Identity with { Observer = new(2) } }));
                Require(candidate.ViewportBytes == 0, "observer replacement releases all retained viewport rasters");

                void Capture(MapRedraw redraw, int index)
                {
                    var visual = new VisualFrame(new([new EffectFrame(effect, index * .12, effectPoint.X)]), new([]),
                        new(["Viewport notice"]), (index & 1) == 0);
                    reference.InvalidateFrame();
                    var result = Paint(candidate, target, epoch);
                    Paint(reference, expected, fresh);
                    if (Pixels(target) != Pixels(expected))
                    {
                        Encode(target, Path.Combine(output, "scroll-actual.png"));
                        Encode(expected, Path.Combine(output, "scroll-expected.png"));
                        throw new InvalidOperationException($"{density}× {(flat ? "flat" : "wrapped")} scroll frame {index} differs from a fresh image");
                    }
                    Require(result.Redraw == redraw, $"{density}× {(flat ? "flat" : "wrapped")} frame {index} expects {redraw}, drew {result.Redraw}");
                    if (redraw == MapRedraw.Scrolled)
                        Require(result.GroundDamagePixels > 0 && result.GroundDamagePixels < (long)width * height * density * density / 8,
                            "small pan repaints only a narrow terrain strip");
                    using var pixelsOnScreen = target.PeekPixels();
                    Require(pixelsOnScreen.GetPixelColor(0, 0) == new SKColor(0xff423c38), "map drawing leaves the host margin intact");
                    samples.Add(new { flat, density, camera, redraw = result.Redraw.ToString(), result.GroundDamagePixels, result.PreparedChunks });

                    MapDrawStats Paint(MapPainter painter, SKSurface image, long revision)
                    {
                        image.Canvas.Clear(new(0xff423c38));
                        using var restore = new SKAutoCanvasRestore(image.Canvas, true);
                        image.Canvas.ClipRect(SKRect.Create(border, border, width * density, height * density));
                        image.Canvas.Translate(border, border);
                        image.Canvas.Scale(density);
                        return painter.Draw(image.Canvas, revision, camera, width, height, density, pick, display, visual);
                    }
                }
            }

        int viewportAllocations = 0;
        using (var limited = new MapPainter(atlas, fonts, allocateRaster: size =>
            size.Width == width && size.Height == height && ++viewportAllocations > 2 ? null : SKSurface.Create(size)))
        using (var target = SKSurface.Create(new SKImageInfo(width, height)))
        using (var expected = SKSurface.Create(new SKImageInfo(width, height)))
        {
            var camera = new MapCamera(25, 31, 1);
            long initial = limited.SetScene(new(stock));
            limited.Draw(target.Canvas, initial, camera, width, height, 1, null);
            camera = camera.Pan(5, 3, stock, width, height);
            reference.InvalidateFrame();
            long fresh = reference.SetScene(new(stock));
            reference.Draw(expected.Canvas, fresh, camera, width, height, 1, null);
            Require(limited.Draw(target.Canvas, initial, camera, width, height, 1, null).Redraw == MapRedraw.Full &&
                Pixels(target) == Pixels(expected), "denied scroll buffer falls back to a correct fresh ground raster");
        }
        File.WriteAllText(Path.Combine(output, "MV29-scroll.json"), JsonSerializer.Serialize(samples, Json));
    }
}
