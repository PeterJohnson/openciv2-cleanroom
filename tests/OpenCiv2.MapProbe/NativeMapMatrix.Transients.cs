using OpenCiv2.App.Map;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void RouteOverlayChecks(MapPainter painter, string output)
    {
        const int width = 900, height = 480, columns = 16, rows = 24;
        var actor = MapGallery.Open("MV24").Map.Units.First(u => u.Target is not null) with { Position = new(2, 4) };
        var other = actor with { Position = new(4, 20), Target = new(new(20), new(5)) };
        var plain = new ObservedTile(1, default, false, 0, false);
        var tiles = Enumerable.Repeat(plain, columns * rows).ToArray();
        var world = MapGallery.Open("MV20").Map with
        {
            Width = columns * 2,
            Height = rows,
            Flat = true,
            Tiles = new(tiles),
            Units = new([actor, other]),
            Cities = new([]),
            Annotations = MapAnnotations.Stock with { ActiveUnit = actor.Target }
        };
        var paths = new OwnedList<MapPath>(
        [
            new(MapPathKind.ActiveRoute, new([actor.Position, new(12, 4)]), actor.Target),
            new(MapPathKind.TradePartner, new([new(2, 8), new(12, 8)])),
            new(MapPathKind.TradeBonusSatisfied, new([new(2, 12), new(12, 12)])),
            new(MapPathKind.TradeBonusUnsatisfied, new([new(2, 16), new(12, 16)]))
        ]);
        var camera = new MapCamera(48, 60, 1);
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        using var ground = SKSurface.Create(new SKImageInfo(width, height));
        Draw(ground, world, camera);
        var routed = world with { Annotations = world.Annotations with { Paths = paths } };
        Draw(surface, routed, camera);
        CheckColor(4, 0xffead88a);
        CheckColor(8, 0xff7bcfda);
        CheckColor(12, 0xff98d666);
        CheckColor(16, 0xffed8d69, dashed: true);
        Encode(surface, Path.Combine(output, "MV20-four-paths.png"));
        Draw(surface, routed with { Annotations = routed.Annotations with { ActiveUnit = other.Target } }, camera);
        Require(SameStrip(surface, ground, camera, 4, 5, 9), "active change clears the old route in the retained frame");
        CheckColor(8, 0xff7bcfda);
        var replacement = actor with { Target = new(actor.Target!.Value.Slot, new(actor.Target.Value.Token.Value + 1)) };
        var stale = routed with { Units = new([replacement, other]), Annotations = routed.Annotations with { ActiveUnit = replacement.Target } };
        Draw(surface, stale, camera);
        Require(SameStrip(surface, ground, camera, 4, 5, 9), "a recycled slot cannot keep drawing the old target's route");
        Draw(surface, routed with { Units = new([actor with { Position = new(2, 2) }, other]) }, camera);
        Require(SameStrip(surface, ground, camera, 4, 5, 9), "a unit's changed position invalidates a route starting at the old tile");
        var unbound = world with { Annotations = world.Annotations with { Paths = new([paths[0] with { Target = null }]) } };
        Draw(surface, unbound, camera);
        Require(Pixels(surface) == Pixels(ground), "an unbound active route is omitted");
        var empty = world with { Annotations = world.Annotations with { Paths = new([paths[1] with { Points = new([]) }]) } };
        Draw(surface, empty, camera);
        Require(Pixels(surface) == Pixels(ground), "an empty trade path has no visual effect");

        // One square grazed at a diamond corner breaks a segment between still-known endpoints.
        tiles[7 * columns + 5 / 2] = default;
        var disconnected = new MapPath(MapPathKind.TradePartner, new([new(2, 8), new(4, 8), new(8, 8), new(12, 8)]));
        var withGap = world with { Tiles = new(tiles) };
        Draw(ground, withGap, camera);
        Draw(surface, withGap with { Annotations = withGap.Annotations with { Paths = new([disconnected]) } }, camera);
        Require(SameStrip(surface, ground, camera, 8, 5, 7.5) &&
            CountColor(surface, camera, 8, 2.3, 3.7, new(0xff7bcfda)) > 10 &&
            CountColor(surface, camera, 8, 8.25, 11.75, new(0xff7bcfda)) > 10,
            "an undisclosed corner removes its segment and preserves the two admitted pieces");
        Encode(surface, Path.Combine(output, "MV20-disclosed-disconnected.png"));

        var admitted = new MapRegion(new([new(2, 8), new(3, 7), new(3, 9), new(4, 8)]));
        var restricted = world with { Annotations = world.Annotations with { Region = admitted } };
        Draw(ground, restricted, camera);
        Draw(surface, restricted with { Annotations = restricted.Annotations with { Paths = new([disconnected]) } }, camera);
        Require(CountColor(surface, camera, 8, 2.3, 3.7, new(0xff7bcfda)) > 10 &&
            SameStrip(surface, ground, camera, 8, 4.25, 11), "map-region clipping retains only the admitted segment");

        var wrapped = world with { Flat = false };
        var seam = new MapCamera(120 - columns * 2 * 32, 60, 1);
        var acrossSeam = disconnected with { Points = new([new(columns * 2 - 2, 8), new(0, 8)]) };
        Draw(ground, wrapped, seam);
        Draw(surface, wrapped with { Annotations = wrapped.Annotations with { Paths = new([acrossSeam]) } }, seam);
        Require(CountColor(surface, seam, 8, 30.25, 31.75, new(0xff7bcfda)) > 10 &&
            SameStrip(surface, ground, seam, 8, 32.25, 40), "wrapped trade line uses the nearest segment without painting a long return");
        Encode(surface, Path.Combine(output, "MV20-wrapped-path.png"));

        void Draw(SKSurface target, MapObservation observation, MapCamera position)
        {
            long epoch = painter.SetScene(new(observation));
            painter.Draw(target.Canvas, epoch, position, width, height, 1, null, visual: VisualFrame.Empty);
        }

        void CheckColor(int y, uint rgb, bool dashed = false)
        {
            int solid = CountColor(surface, camera, y, 4, 11, new(rgb));
            Require(solid > (dashed ? 80 : 350), $"route {y} has its specified {rgb:x8} foreground");
            if (dashed) Require(solid < 355, "unsatisfied route leaves visible dash gaps");
        }
    }

    private static void FocusAndStackChecks(MapPainter painter, string output)
    {
        const int width = 576, height = 390, columns = 7, rows = 12;
        var camera = new MapCamera(44, 90, 1);
        var board = MapGallery.Open("MV24").Map;
        var origin = new MapPoint(6, 6);
        var destination = new MapPoint(10, 6);
        var actor = board.Units.First(u => u.Target is not null) with { Position = origin };
        var second = actor with { Type = new(2), Target = new(new(20), new(6)) };
        var foreign = new ObservedMapUnit(origin, new(8), new(2), null, MapUnitDetails.Withheld);
        var selectedUnit = foreign with { Position = new(10, 8) };
        var plain = new ObservedTile(1, default, false, 0, false);
        var world = board with
        {
            Width = columns * 2,
            Height = rows,
            Flat = true,
            Tiles = new(Enumerable.Repeat(plain, columns * rows)),
            Units = new([actor, second, foreign, selectedUnit]),
            Cities = new([]),
            Annotations = MapAnnotations.Stock with { ActiveUnit = actor.Target }
        };
        var scene = new MapScene(world);
        using var target = SKSurface.Create(new SKImageInfo(width, height));
        var inspected = new MapPick(new(4, 8), MapPickKind.Tile);
        var selected = new MapPick(selectedUnit.Position, MapPickKind.Unit);
        var terrain = new MapPick(new(12, 8), MapPickKind.Tile);
        long epoch = painter.SetScene(scene);
        int selectedX = (int)camera.ScreenX(terrain.Position.X), selectedY = (int)camera.ScreenY(terrain.Position.Y);
        painter.Draw(target.Canvas, epoch, camera, width, height, 1, null, visual: VisualFrame.Empty);
        SKColor ground = ReadPixel();
        painter.Draw(target.Canvas, epoch, camera, width, height, 1, terrain, visual: VisualFrame.Empty);
        SKColor highlighted = ReadPixel();
        Require(highlighted != ground && highlighted != SKColors.White && highlighted.Red > ground.Red,
            "the pinned selection mask preserves visible terrain color");
        string off = Draw(VisualFrame.Empty);
        var stable = VisualFrame.Empty with { Pulse = true };
        string on = Draw(stable);
        var activeInk = new SKColor(0xffe17d57);
        var inspectedInk = new SKColor(0xff88c8cd);
        var selectionInk = new SKColor(0xff000000 | MapArtCatalog.Stock.DisplayColors["selectionHighlight"]);
        Require(off != on && RegionColor(target, camera, origin, activeInk) > 0 &&
            RegionColor(target, camera, inspected.Position, inspectedInk) > 0 &&
            RegionColor(target, camera, selected.Position, selectionInk) > 0,
            "inspected tile, selected object and pulsing active unit appear at separate coordinates");
        Require(Draw(VisualFrame.Empty, new(ReducedMotion: true)) == on, "reduced motion keeps a stable focus mark");

        var history = new List<(ulong, PresentationEnd)>();
        var timeline = new MapVisualTimeline((id, end) => history.Add((id, end)));
        timeline.Bind(world.Identity);
        var move = new MapEffect(1, MapEffectKind.Move, origin, destination, 320, actor);
        Require(!timeline.Queue(world.Identity, move with { Unit = null }, 0) && timeline.Queue(world.Identity, move, 0),
            "actorless movement is rejected without swallowing a valid presentation ID");
        Require(timeline.Queue(world.Identity, new MapEffect(2, MapEffectKind.CombatParticipant, destination, destination, 500), 0),
            "combat participant admitted independently of the moving actor");
        var marked = timeline.Sample(scene, 160, false);
        var stats = painter.Draw(target.Canvas, epoch, camera, width, height, 1, selected, visual: marked, inspected: inspected);
        var remaining = scene.StacksWithout(marked.Moving)[origin];
        Require(remaining.Unit == second && remaining.Count == 2 && stats.Objects == 3 && stats.Moving == 1,
            "remaining two-unit stack displays its own representative during movement");
        Require(RegionColor(target, camera, new(8, 6), activeInk) > 0 &&
            RegionColor(target, camera, origin, activeInk) == 0 &&
            RegionColor(target, camera, destination, new(0xffff9055)) > 0, "active focus travels and combat emphasis stays at its participant");
        Encode(target, Path.Combine(output, "MV18-moving-active-focus.png"));

        string midpoint = Draw(marked with { Effects = new([marked.Effects[0]]), Pulse = false });
        painter.SetScene(null);
        var staticMidpoint = world with { Units = new([actor with { Position = new(8, 6) }, second, foreign, selectedUnit]) };
        epoch = painter.SetScene(new(staticMidpoint));
        Require(Draw(VisualFrame.Empty) == midpoint, "moving body, banner and shadow match fresh static art at the midpoint");
        epoch = painter.SetScene(scene);

        Require(Draw(marked, new(ReducedMotion: true)) == on, "the immediate reduced-motion draw restores the settled unit without trails");
        Require(timeline.Sample(scene, 170, true).Effects.Count == 0 &&
            history.SequenceEqual(new[] { (1UL, PresentationEnd.Skipped), (2UL, PresentationEnd.Skipped) }),
            "each effect skipped exactly once");
        Require(timeline.Queue(world.Identity, move with { Id = 3 }, 200) &&
            timeline.Queue(world.Identity, new MapNotice(4, "Cancelled notice", 300), 200), "new presentation after skip");
        timeline.CancelAll();
        timeline.CancelAll();
        Require(history.Skip(2).SequenceEqual(new[] { (3UL, PresentationEnd.Cancelled), (4UL, PresentationEnd.Cancelled) }) &&
            timeline.Sample(scene, 240, false).Notices.Count == 0, "batch cancellation completes each entry once");
        var replacement = actor with { Target = new(actor.Target!.Value.Slot, new(actor.Target.Value.Token.Value + 1)) };
        var revised = new MapScene(world with { Units = new([replacement, second, foreign, selectedUnit]) });
        Require(timeline.Queue(world.Identity, move with { Id = 5 }, 300), "moving actor queued before the slot is recycled");
        var cleared = timeline.Sample(revised, 320, false);
        Require(cleared.Moving.Count == 0 && cleared.Effects.Count == 0 && history.Last() == (5UL, PresentationEnd.Interrupted),
            "slot reuse interrupts the specific old presentation target");
        epoch = painter.SetScene(revised);
        Draw(stable);
        Require(RegionColor(target, camera, origin, activeInk) == 0, "a recycled slot cannot inherit the old active focus");
        Require(world.Units[0] == actor && world.Units[1] == second && world.Units[2] == foreign, "visual stack choice leaves the owned units ordered");

        string Draw(VisualFrame frame, MapDisplay display = default)
        {
            painter.Draw(target.Canvas, epoch, camera, width, height, 1, selected, display, frame, inspected);
            return Pixels(target);
        }

        SKColor ReadPixel()
        {
            using var pixels = target.PeekPixels();
            return pixels.GetPixelColor(selectedX, selectedY);
        }
    }

    private static bool SameStrip(SKSurface a, SKSurface b, MapCamera camera, int row, double left, double right)
    {
        using var first = a.PeekPixels();
        using var second = b.PeekPixels();
        for (int y = (int)camera.ScreenY(row) - 2; y <= camera.ScreenY(row) + 2; y++)
            for (int x = (int)(camera.X + left * 32); x < camera.X + right * 32; x++)
                if (first.GetPixelColor(x, y) != second.GetPixelColor(x, y)) return false;
        return true;
    }

    private static int CountColor(SKSurface surface, MapCamera camera, int row, double first, double last, SKColor expected)
    {
        using var pixels = surface.PeekPixels();
        int count = 0;
        for (int y = (int)camera.ScreenY(row) - 1; y <= camera.ScreenY(row); y++)
            for (int x = (int)(camera.X + first * 32); x < camera.X + last * 32; x++)
                if (pixels.GetPixelColor(x, y) == expected) count++;
        return count;
    }

    private static int RegionColor(SKSurface surface, MapCamera camera, MapPoint point, SKColor expected)
    {
        using var pixels = surface.PeekPixels();
        int x = (int)camera.ScreenX(point.X), y = (int)camera.ScreenY(point.Y), count = 0;
        for (int py = y - 16; py <= y + 16; py++)
            for (int px = x - 32; px <= x + 32; px++)
                if (pixels.GetPixelColor(px, py) == expected) count++;
        return count;
    }
}
