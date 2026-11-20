using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void KnowledgeBoundaryPixels(AssetAtlas atlas, PinnedFonts fonts, MapArtSelection? replacement, string output)
    {
        string theme = replacement is null ? "stock" : "classic";
        var map = MapGallery.Open("MV09").Map;
        using var painter = new MapPainter(atlas, fonts, replacement);
        using var textures = new MapTextures(atlas, replacement);
        using var opacity = SKSurface.Create(new SKImageInfo(64, 32));
        var opacitySamples = new List<uint[]>();
        foreach (var substrate in new[] { SKColors.Magenta, SKColors.Lime })
        {
            opacity.Canvas.Clear(substrate);
            PaintGroundTile(opacity.Canvas, textures, "fog/full", 0, 0);
            opacitySamples.Add(ReadMapPixels(opacity));
        }
        int interior = 0;
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 64; x++)
            {
                float distance = MathF.Abs(x + .5f - 32) / 32 + MathF.Abs(y + .5f - 16) / 16;
                if (distance >= .96f) continue;
                int offset = y * 64 + x;
                Require(opacitySamples[0][offset] == opacitySamples[1][offset],
                    $"{theme} unknown tile is opaque throughout its diamond interior");
                interior++;
            }

        var samples = new List<object>();
        foreach (double zoom in new[] { 1d, .5 })
        {
            int width = (int)(1860 * zoom), height = (int)(450 * zoom);
            var camera = new MapCamera(42 * zoom, 34 * zoom, zoom);
            using var actual = SKSurface.Create(new SKImageInfo(width, height));
            using var reference = SKSurface.Create(new SKImageInfo(width, height));
            var layerPlan = PlanTerrainReference(map, camera, width);
            for (int mask = 0; mask < 16; mask++)
            {
                var center = new MapPoint(4 + mask % 8 * 6, 4 + mask / 8 * 6);
                var expected = Enumerable.Range(0, 4).Where(q => (mask & (1 << q)) != 0).Select(q => $"fog/{q}");
                Require(layerPlan.Where(layer => layer.Position == center && layer.Pass == TerrainPass.Fog)
                    .Select(layer => layer.Key).SequenceEqual(expected), $"fog intersection {mask}");
            }
            var scene = new MapScene(map);
            painter.Draw(actual.Canvas, painter.SetScene(scene), camera, width, height, 1, null);
            var pixels = ReadMapPixels(actual);
            DrawTerrainReference(reference.Canvas, textures, layerPlan, camera);
            int differences = DifferentMapPixels(pixels, ReadMapPixels(reference));
            string filename = $"MV09-fog-{theme}-{(zoom == 1 ? "full" : "reduced")}.png";
            if (differences != 0)
                Encode(reference, Path.Combine(output, filename.Replace(".png", "-reference.png", StringComparison.Ordinal)));
            Encode(actual, Path.Combine(output, filename));
            Require(differences == 0, $"{theme} fog at {zoom}: {differences} pixels differ from the independent layout");
            var featherPixels = new Dictionary<string, int>();
            for (int q = 0; q < 4; q++)
            {
                string key = $"fog/{q}";
                DrawTerrainReference(reference.Canvas, textures, layerPlan.Where(layer => layer.Key != key), camera);
                int changed = DifferentMapPixels(pixels, ReadMapPixels(reference));
                Require(changed > 0, $"{theme} fog feather {q} contributes visible pixels at {zoom}");
                featherPixels.Add(key, changed);
            }
            samples.Add(new { camera, width, height, interior, intersections = 16, featherPixels, filename, sha256 = FileHash(Path.Combine(output, filename)) });
        }
        File.WriteAllText(Path.Combine(output, $"MV09-fog-{theme}.json"), JsonSerializer.Serialize(samples, Json));
    }

    private static void RegionReplayChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        var board = MapGallery.Open("MV21").Map;
        var actor = board.Units[0];
        var excluded = board.Units[1];
        var center = actor.Position;
        var region = new MapRegion(new([center, new(2, 4), new(3, 3), new(5, 3), new(3, 5), new(5, 5)]));
        var city = board.Cities[0] with { Position = excluded.Position };
        var tiles = board.Tiles.ToArray();
        int index = center.Y * (board.Width / 2) + center.X / 2;
        tiles[index] = tiles[index] with { Improvements = MapImprovements.Road };
        tiles[index + 1] = tiles[index + 1] with { Improvements = MapImprovements.Railroad | MapImprovements.CitySite };
        var baseline = board with
        {
            Tiles = new(tiles),
            Cities = new([city, board.Cities[1]]),
            Annotations = board.Annotations! with { Region = region }
        };
        var unrestricted = new MapScene(baseline with { Annotations = baseline.Annotations! with { Region = null } });
        Require(unrestricted.InCityRadius(center) && unrestricted.HasTarget(excluded.Target!.Value),
            "the adjacent city and worker affect the unrestricted view");
        var substitutedTiles = tiles.ToArray();
        substitutedTiles[index + 1] = new(10, MapImprovements.Pollution, true, 2, false);
        var alternate = baseline with
        {
            Revision = baseline.Revision + 1,
            Tiles = new(substitutedTiles),
            Units = new(baseline.Units.Select(unit => unit.Target == excluded.Target
                ? unit with { Type = new(35), Details = unit.Details with { Work = MapFact<int>.Known(254) } } : unit)),
            Cities = new([board.Cities[1]])
        };
        var replay = baseline with { Revision = alternate.Revision + 1 };
        var selection = new MapPick(center, MapPickKind.Unit);
        var stalePick = new MapPick(excluded.Position, MapPickKind.Unit);
        const int width = 470, height = 250;
        var display = new MapDisplay(Grid: true, CityRadius: true, Ownership: true);
        using var painter = new MapPainter(atlas, fonts);
        var samples = new List<object>();
        foreach (bool flat in new[] { true, false })
            foreach (var (zoom, density) in new[] { (1d, 1d), (.78, 2d), (.5, 2d), (.3, 1d) })
            {
                using var target = SKSurface.Create(new SKImageInfo((int)(width * density), (int)(height * density)));
                using var overview = SKSurface.Create(new SKImageInfo(240, 110));
                target.Canvas.Scale((float)density);
                int wrap = flat ? 0 : board.Width;
                var camera = new MapCamera((flat ? 42 : 72) - wrap * 32 * zoom, 46, zoom);
                double centerX = camera.ScreenX(center.X + wrap), excludedX = camera.ScreenX(excluded.Position.X + wrap);
                Require(centerX > 0 && excludedX < width, "both inspection anchors are inside the viewport");
                string? expected = null, expectedOverview = null;
                var redraws = new List<string>();
                foreach (var observation in new[] { baseline, alternate, replay })
                {
                    var scene = new MapScene(observation with { Flat = flat });
                    var visible = scene.Pick(camera, centerX, camera.ScreenY(center.Y));
                    Require(visible == selection &&
                        scene.Pick(camera, excludedX, camera.ScreenY(excluded.Position.Y)) is null,
                        "clipped region admits the own worker and rejects a neighboring unit and city");
                    string description = MapInspection.Describe(scene, selection);
                    Require(description.Contains("Accumulated work 0", StringComparison.Ordinal) &&
                        MapInspection.Describe(scene, selection, excluded.Target) == description &&
                        MapInspection.Describe(scene, stalePick) == "" &&
                        MapInspection.Describe(scene, new(center, MapPickKind.City)) == "",
                        "out-of-region target or stale city pick cannot supply inspection details");
                    Require(!scene.InCityRadius(center) && MapOverviewGeometry.Describe(scene, excluded.Position) is null,
                        "an excluded city cannot create a radius or city tooltip inside the clipped view");
                    var art = new List<TileSprite>();
                    MapTileArt.Select(art, scene, center, MapDetail.Full, false);
                    Require(art.Any(sprite => sprite.Key == "road/0") && !art.Any(sprite => sprite.Key == "road/2"),
                        "excluded improvements and cities cannot complete the admitted road connection");
                    var timeline = new MapVisualTimeline();
                    timeline.Bind(observation.Identity);
                    timeline.Queue(observation.Identity, new MapEffect(1, MapEffectKind.Explosion, excluded.Position, excluded.Position, 250), 0);
                    var visual = timeline.Sample(scene, 72, false);
                    Require(visual.Effects.Count == 0, "excluded effects do not generate a frame");
                    long epoch = painter.SetScene(scene);
                    var draw = painter.Draw(target.Canvas, epoch, camera, width, height, density, selection, display, visual);
                    Require(draw.Error is null && draw.Labels == 0 && draw.Objects > 0, "region draws the worker and omits excluded city labels");
                    painter.DrawOverview(overview.Canvas, epoch, 240, 110, camera, width, height, center);
                    string main = Pixels(target), reduced = Pixels(overview);
                    expected ??= main;
                    expectedOverview ??= reduced;
                    Require(expected == main && expectedOverview == reduced, "substituted and replayed region inputs preserve pixels");
                    using var fresh = new MapPainter(atlas, fonts);
                    fresh.Draw(target.Canvas, fresh.SetScene(scene), camera, width, height, density, selection, display, visual);
                    Require(Pixels(target) == main, "retained region matches a new painter");
                    redraws.Add(draw.Redraw.ToString());
                }
                Require(redraws.Skip(1).All(kind => kind == nameof(MapRedraw.Cached)),
                    "substituted and replayed region observations reuse the unchanged viewport");
                string? filename = null;
                if (zoom == .78)
                {
                    filename = $"MV09-region-{(flat ? "flat" : "wrapped")}-replay.png";
                    Encode(target, Path.Combine(output, filename));
                }
                samples.Add(new { flat, zoom, density, camera, redraws, main = expected, overview = expectedOverview, filename });
            }
        File.WriteAllText(Path.Combine(output, "MV09-region-replay.json"), JsonSerializer.Serialize(new
        {
            region,
            center,
            excluded = excluded.Position,
            baseline.Identity,
            samples
        }, Json));
    }

    private static void WorkProjectionChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        using var session = new SeamSession();
        var runtime = session.Runtime;
        var world = runtime.World;
        var rules = runtime.Process.Rules.Definitions.Units;
        rules[0] = new() { Identity = new("settlers", RuleText.Empty), Role = RuleRole.Settle, HitPoints = 10 };
        rules[1] = new() { Identity = new("engineers", RuleText.Empty), Role = RuleRole.Settle, HitPoints = 10 };
        var units = world.Entities.Units.Storage;
        units[0].Type = new(0);
        units[0].Orders = UnitOrder.Road;
        units[1].Type = new(1);
        units[1].Orders = UnitOrder.Irrigate;
        units[1].Counter = 9;
        units[2].Counter = 117;
        units[4].Type = new(1);
        units[4].Counter = 45;
        var original = Publish();
        Require(Own(original, 0).Details.Work.Value == 0 && Own(original, 1).Details.Work.Value == 9 &&
            Own(original, 2).Details.Work.State == MapFactState.Unavailable,
            "scoped work uses the admitted stored value for both roles, with undefined work unavailable");
        var seen = original.Units.Single(item => item.Target is null);
        Require(seen.Details.Work.State == MapFactState.Withheld, "detected foreign engineer has no published work");
        units[3].Counter = 253;
        units[4].Counter = 215;
        var hidden = Publish();
        Require(original.Units.Equals(hidden.Units), "foreign stored counter changes do not enter the worker projection");
        units[0].Counter = 231;
        var changed = Publish();
        var worker = Own(changed, 0);
        var pick = new MapPick(worker.Position, MapPickKind.Unit);
        Require(worker.Details.Work.Value == 231 && Own(original, 0).Details.Work.Value == 0,
            "own published large work leaves the original owned snapshot unchanged");
        var scene = new MapScene(changed);
        string workerText = MapInspection.Describe(scene, pick);
        string engineerText = MapInspection.Describe(scene, pick, Own(changed, 1).Target);
        string unavailableText = MapInspection.Describe(scene, pick, Own(changed, 2).Target);
        string foreignText = MapInspection.Describe(scene, new(seen.Position, MapPickKind.Unit));
        Require(workerText.Contains("Accumulated work 231", StringComparison.Ordinal) &&
            engineerText.Contains("Accumulated work 9", StringComparison.Ordinal) &&
            unavailableText.Contains("Work counter unavailable", StringComparison.Ordinal) &&
            foreignText.Contains("Work counter withheld", StringComparison.Ordinal) &&
            !workerText.Contains("turn", StringComparison.OrdinalIgnoreCase), "inspection shows only admitted accumulated work");
        Require(MapInspection.Describe(scene, new(seen.Position, MapPickKind.Unit), worker.Target) == foreignText,
            "a worker target from another tile cannot disclose work in foreign hover");

        var game = runtime.GameplayRandom.Capture();
        var presentation = runtime.PresentationRandom.Capture();
        var stored = world.Entities.Units.Scanned.ToArray();
        var samples = new List<object>();
        using var painter = new MapPainter(atlas, fonts);
        foreach (double density in new[] { 1d, 2d })
        {
            const int width = 410, height = 255;
            var camera = new MapCamera(46, 42, 1.15);
            using var image = SKSurface.Create(new SKImageInfo((int)(width * density), (int)(height * density)));
            image.Canvas.Scale((float)density);
            uint[] zero = DrawWork(original);
            Require(DifferentMapPixels(zero, DrawWork(hidden)) == 0, "hidden foreign counters cannot change worker pixels");
            uint[] large = DrawWork(changed);
            int different = DifferentMapPixels(zero, large);
            Require(different > 0, "published large work has glyph pixels distinct from known zero");
            string filename = $"MV21-work-{density}x.png";
            Encode(image, Path.Combine(output, filename));
            samples.Add(new { density, different, filename, sha256 = FileHash(Path.Combine(output, filename)) });

            uint[] DrawWork(MapObservation observation)
            {
                var renderScene = new MapScene(observation);
                var draw = painter.Draw(image.Canvas, painter.SetScene(renderScene), camera, width, height, density, pick);
                Require(draw.Error is null, "worker badge renders successfully");
                return ReadMapPixels(image);
            }
        }
        Require(world.Entities.Units.Scanned.SequenceEqual(stored) && runtime.GameplayRandom.Capture() == game &&
            runtime.PresentationRandom.Capture() == presentation && session.Player.ObserveMap()!.Revision == changed.Revision,
            "work drawing and inspection leave the world, revision and both random streams untouched");
        File.WriteAllText(Path.Combine(output, "MV21-work-projection.json"), JsonSerializer.Serialize(new
        {
            original.Identity,
            before = original.Revision,
            foreignChanged = hidden.Revision,
            after = changed.Revision,
            workerText,
            engineerText,
            unavailableText,
            foreignText,
            samples
        }, Json));

        MapObservation Publish()
        {
            runtime.Publish();
            return session.Player.ObserveMap()!;
        }

        static ObservedMapUnit Own(MapObservation map, int slot) => map.Units.Single(unit => unit.Target?.Slot.Value == slot);
    }
}
