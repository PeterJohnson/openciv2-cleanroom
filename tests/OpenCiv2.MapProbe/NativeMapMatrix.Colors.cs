using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static void OwnerColorPixelChecks(AssetAtlas atlas, PinnedFonts fonts, MapArtSelection? art, string output)
    {
        const int width = 1700, height = 570, overviewWidth = 240, overviewHeight = 176;
        const byte observer = 1;
        var camera = new MapCamera(42, 45, 1);
        var overviewCamera = new MapCamera(-300, -410, 1);
        var board = MapGallery.Open("MV17");
        var map = board.Map;
        var unit = map.Units.First(unit => unit.Owner.Value == observer);
        var north = map.Cities.First(city => city.Details!.Owner.Value == observer);
        var south = map.Cities.Last(city => city.Details!.Owner.Value == observer);
        var airbase = new MapPoint(10, 22);
        var territory = new MapPoint(10, 28);
        var radius = new MapPoint(8, 10);
        var unitPick = new MapPick(unit.Position, MapPickKind.Unit);
        var cityPick = new MapPick(north.Position, MapPickKind.City);
        var airbasePick = new MapPick(airbase, MapPickKind.Tile);
        var catalog = MapArtCatalog.Stock;
        string theme = art is null ? "stock" : "classic";
        using var textures = new MapTextures(atlas, art);
        using var painter = new MapPainter(atlas, fonts, art);
        using var fresh = new MapPainter(atlas, fonts, art);
        using var overview = SKSurface.Create(new SKImageInfo(overviewWidth, overviewHeight));
        var shield = textures.Marker("unit/1", catalog.Units[1].Shield);
        var flag = textures.Flag("city/0", catalog.FlagAnchor);
        var population = textures.Marker("city/0", catalog.Cities[0].Population);
        var samples = new List<object>();

        foreach (double density in new[] { 1d, 2d })
        {
            int backingWidth = (int)(width * density), backingHeight = (int)(height * density);
            using var actual = SKSurface.Create(new SKImageInfo(backingWidth, backingHeight));
            using var reference = SKSurface.Create(new SKImageInfo(backingWidth, backingHeight));
            actual.Canvas.Scale((float)density);
            reference.Canvas.Scale((float)density);
            var regions = new Dictionary<string, SKRectI>
            {
                ["brightFlag"] = Box(north.Position, flag.X - 32, flag.Y - 32, 14, 22),
                ["darkFlag"] = Box(south.Position, flag.X - 32, flag.Y - 32, 14, 22),
                ["disorder"] = Box(north.Position, -16, -26, 30, 30),
                ["population"] = Box(north.Position, population.X - 32, population.Y - 32, 12, 12),
                ["caption"] = Box(south.Position, -45, 17, 90, 7),
                ["airbase"] = Box(airbase, -32, -32, 64, 48),
                ["ownership"] = Box(territory, -5, -5, 10, 10),
                ["radius"] = Box(radius, -32, -16, 64, 32),
            };
            int shieldWidth = (int)(12 * density), shieldHeight = (int)(20 * density);
            using var shieldMask = SKSurface.Create(new SKImageInfo(shieldWidth, shieldHeight));
            shieldMask.Canvas.DrawImage(textures.Get(catalog.Shield, density), SKRect.Create(shieldWidth, shieldHeight),
                new SKSamplingOptions(SKFilterMode.Linear));
            using var coverage = shieldMask.PeekPixels();
            int offset = (int)((shield.X < 32 ? -4 : 4) * density);
            var shieldBox = Box(unit.Position, shield.X - 32, shield.Y - 32, 12, 20);
            var primaryPixels = new List<(SKPointI Point, byte Alpha)>();
            var secondaryPixels = new List<(SKPointI Point, byte Alpha)>();
            for (int y = (int)(9 * density); y < shieldHeight; y++)
                for (int x = Math.Min(0, offset); x < shieldWidth + Math.Max(0, offset); x++)
                {
                    var position = new SKPointI(shieldBox.Left + x, shieldBox.Top + y);
                    byte primary = Alpha(x, y), secondary = Alpha(x - offset, y);
                    if (primary > 0 && secondary == 0) primaryPixels.Add((position, primary));
                    if (primary == 0 && secondary > 0) secondaryPixels.Add((position, secondary));
                }
            Require(primaryPixels.Count > 0 && secondaryPixels.Count > 0, "source shield masks provide visible primary and secondary test points");

            var initial = WithRole(7, 0);
            painter.Draw(actual.Canvas, painter.SetScene(new(initial)), camera, width, height, density, unitPick, board.Display);
            var previous = ReadMapPixels(actual);
            var originalRoles = initial.Annotations!.FactionRoles.ToArray();
            var known = new MapScene(initial);
            string unitText = MapInspection.Describe(known, unitPick);
            string cityText = MapInspection.Describe(known, cityPick);
            string airbaseText = MapInspection.Describe(known, airbasePick);
            Require(unitText.Contains("Civilization 1", StringComparison.Ordinal) && cityText.Contains("Civilization 1", StringComparison.Ordinal) &&
                airbaseText.Contains("Aircraft of Civilization 1", StringComparison.Ordinal), "owners are identifiable in inspection");

            for (byte role = 0; role < 8; role++)
            {
                using var previousImage = actual.Snapshot();
                using var previousPixels = previousImage.PeekPixels();
                var observation = WithRole(role, (ulong)role + 1);
                var scene = new MapScene(observation);
                long epoch = painter.SetScene(scene);
                var stats = painter.Draw(actual.Canvas, epoch, camera, width, height, density, unitPick, board.Display);
                var current = ReadMapPixels(actual);
                Require(stats.Redraw == MapRedraw.Dirty && stats.PreparedChunks == 0,
                    $"{theme} role {role} invalidates dependent foreground without rebuilding terrain");
                fresh.InvalidateFrame();
                fresh.Draw(reference.Canvas, fresh.SetScene(scene), camera, width, height, density, unitPick, board.Display);
                int differences = DifferentMapPixels(current, ReadMapPixels(reference));
                if (differences != 0)
                {
                    Encode(actual, Path.Combine(output, $"MV17-{theme}-dirty.png"));
                    Encode(reference, Path.Combine(output, $"MV17-{theme}-reference.png"));
                }
                Require(differences == 0, $"{theme} role {role} at {density}× matches fresh foreground ({differences} differing pixels)");
                var changes = regions.ToDictionary(entry => entry.Key, entry => CountPixelChanges(previous, current, backingWidth, entry.Value));
                using var pixels = actual.PeekPixels();
                var shieldColors = textures.ShieldColors(role);
                var oldShieldColors = textures.ShieldColors((role + 7) % 8);
                changes.Add("primaryShield", Recolored(primaryPixels, new(0xff000000 | oldShieldColors.Primary), new(0xff000000 | shieldColors.Primary)));
                changes.Add("secondaryShield", Recolored(secondaryPixels, new(0xff000000 | oldShieldColors.Secondary), new(0xff000000 | shieldColors.Secondary)));
                Require(changes.Values.All(pixels => pixels > 0),
                    $"{theme} role {role} at {density}× updates every dependent layer ({string.Join(", ", changes.Select(pair => $"{pair.Key}={pair.Value}"))})");
                Require(CountPixelChanges(previous, current, backingWidth, Box(new(16, 16), -60, -55, 120, 90)) == 0,
                    "the neighboring civilization's city, flag and label keep their pixels");
                painter.DrawOverview(overview.Canvas, epoch, overviewWidth, overviewHeight, overviewCamera, 150, 88, null);
                var overviewPoint = MapOverviewGeometry.CenterOf(map, MapOverviewGeometry.Inner(map, overviewWidth, overviewHeight), north.Position);
                using var overviewPixels = overview.PeekPixels();
                Require(overviewPixels.GetPixelColor((int)overviewPoint.X, (int)overviewPoint.Y) == new SKColor(0xff000000 | textures.Color(role)) &&
                    MapOverviewGeometry.Describe(scene, north.Position) == "Civilization 1 city", "overview color and identity follow the same admitted city");
                Require(MapInspection.Describe(scene, unitPick) == unitText && MapInspection.Describe(scene, cityPick) == cityText &&
                    MapInspection.Describe(scene, airbasePick) == airbaseText && observation.Units.Equals(initial.Units) &&
                    observation.Cities.Equals(initial.Cities) && observation.Tiles.Equals(initial.Tiles) &&
                    observation.Annotations!.FactionRoles.Where((_, slot) => slot != observer).SequenceEqual(originalRoles.Where((_, slot) => slot != observer)),
                    "recolored image keeps the same civilization slots and semantic facts");
                Require(painter.Draw(actual.Canvas, epoch, camera, width, height, density, unitPick, board.Display).Redraw == MapRedraw.Cached &&
                    DifferentMapPixels(current, ReadMapPixels(actual)) == 0, "unchanged color reuses correct viewport pixels");
                string? filename = null;
                if (role is 0 or 7)
                {
                    filename = $"MV17-{theme}-role{role}-{density}x.png";
                    Encode(actual, Path.Combine(output, filename));
                }
                samples.Add(new { role, density, changes, color = textures.Color(role), filename, hash = Pixels(actual) });

                previous = current;

                int Recolored(List<(SKPointI Point, byte Alpha)> positions, SKColor oldColor, SKColor color)
                {
                    int count = 0;
                    foreach (var (point, alpha) in positions)
                    {
                        var old = previousPixels.GetPixelColor(point.X, point.Y);
                        var actualColor = pixels.GetPixelColor(point.X, point.Y);
                        if (old == actualColor) continue;
                        if (alpha == 255 ? actualColor == color :
                            Blended(old.Red, actualColor.Red, oldColor.Red, color.Red, alpha) &&
                            Blended(old.Green, actualColor.Green, oldColor.Green, color.Green, alpha) &&
                            Blended(old.Blue, actualColor.Blue, oldColor.Blue, color.Blue, alpha))
                            count++;
                    }
                    return count;
                }
            }

            byte Alpha(int x, int y) => x >= 0 && x < shieldWidth ? coverage.GetPixelColor(x, y).Alpha : (byte)0;

            SKRectI Box(MapPoint point, int left, int top, int w, int h)
            {
                int x = (int)((camera.ScreenX(point.X) + left) * density);
                int y = (int)((camera.ScreenY(point.Y) + top) * density);
                return new(x, y, x + (int)(w * density), y + (int)(h * density));
            }
        }
        File.WriteAllText(Path.Combine(output, $"MV17-role-{theme}.json"), JsonSerializer.Serialize(samples, Json));

        MapObservation WithRole(byte role, ulong revision)
        {
            var roles = map.Annotations!.FactionRoles.ToArray();
            roles[observer] = role;
            return map with { Revision = revision, Annotations = map.Annotations with { FactionRoles = new(roles) } };
        }

        // Skia rounds each premultiplied channel when compositing partial coverage.
        static bool Blended(int oldPixel, int pixel, int oldColor, int color, int alpha) =>
            Math.Abs(pixel - oldPixel - (color - oldColor) * alpha / 255d) <= 2;
    }

    private static void ScopedColorChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        using var session = new SeamSession();
        var runtime = session.Runtime;
        var world = runtime.World;
        var rules = runtime.Process.Rules;
        var game = runtime.GameplayRandom.Capture();
        var presentation = runtime.PresentationRandom.Capture();
        rules.Text.Initialize();
        world.Civilizations[1].Tribe = 12;
        world.Civilizations[2].Tribe = 13;
        world.Civilizations[3].Tribe = 14;
        rules.Leaders[12].Color = 6;
        rules.Leaders[13].Color = 5;
        rules.Leaders[14].Color = 11;
        var initial = Publish();
        Require(initial.Annotations!.ColorRole(new(1)) == 6 && initial.Annotations.ColorRole(new(2)) == 5 &&
            initial.Annotations.ColorRole(new(3)) == 3, "scoped projection resolves configured roles only for admitted civilizations");
        using var painter = new MapPainter(atlas, fonts);
        using var surface = SKSurface.Create(new SKImageInfo(470, 310));
        var camera = new MapCamera(42, 43, 1);
        var display = new MapDisplay(Grid: true, Ownership: true, CityRadius: true, ReducedMotion: true);
        var first = Draw(initial);
        rules.Leaders[14].Color = 22;
        var concealed = Publish();
        Require(concealed.Annotations!.FactionRoles.Equals(initial.Annotations.FactionRoles) && Draw(concealed) == first,
            "a concealed leader's unsupported configured color cannot change the scoped map");
        rules.Leaders[12].Color = 7;
        var reconfigured = Publish();
        string updated = Draw(reconfigured);
        Require(reconfigured.Annotations!.ColorRole(new(1)) == 7 && initial.Annotations.ColorRole(new(1)) == 6 &&
            reconfigured.Units.Equals(initial.Units) && reconfigured.Cities.Equals(initial.Cities) && updated != first,
            "live owner recolor changes pixels while retaining observations' owner facts and historical role");
        rules.Leaders[12].Color = 9;
        var invalid = Publish();
        var rejected = painter.Draw(surface.Canvas, painter.SetScene(new(invalid)), camera, 470, 310, 1, null, display);
        Require(rejected.Error == "Unsupported owner color role 9", "unsupported admitted color is diagnosed");
        rules.Leaders[12].Color = 7;
        var recovered = Publish();
        Require(Draw(recovered) == updated, "corrected installed color recovers a byte-identical frame");
        Require(runtime.GameplayRandom.Capture() == game && runtime.PresentationRandom.Capture() == presentation,
            "configured roles and rendering leave gameplay and presentation RNG unchanged");
        File.WriteAllText(Path.Combine(output, "MV17-scoped-colors.json"), JsonSerializer.Serialize(new
        {
            initial.Identity,
            before = initial.Annotations.FactionRoles,
            after = reconfigured.Annotations.FactionRoles,
            revisions = new[] { initial.Revision, concealed.Revision, reconfigured.Revision, invalid.Revision, recovered.Revision },
            first,
            updated,
            rejected.Error
        }, Json));

        MapObservation Publish()
        {
            runtime.Publish();
            return session.Player.ObserveMap()!;
        }

        string Draw(MapObservation observation)
        {
            var stats = painter.Draw(surface.Canvas, painter.SetScene(new(observation)), camera, 470, 310, 1, null, display);
            Require(stats.Error is null, "valid scoped colors draw");
            return Pixels(surface);
        }
    }

    private static void DisplayModePixelChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        const int columns = 7, rows = 10, width = 615, height = 245;
        var board = MapGallery.Open("MV19").Map;
        var plains = new ObservedTile(1, default, false, 0, false);
        var terrain = Enumerable.Repeat(plains, columns * rows).ToArray();
        var camera = new MapCamera(85, 47, 1);
        var city = MapGallery.Open("MV21").Map.Cities[0];
        var map = board with
        {
            Width = columns * 2,
            Height = rows,
            Tiles = new(terrain),
            Units = new([]),
            Cities = new([]),
            Annotations = MapAnnotations.Stock
        };
        using var painter = new MapPainter(atlas, fonts);
        using var textures = new MapTextures(atlas);
        using var actual = SKSurface.Create(new SKImageInfo(width, height));
        using var reference = SKSurface.Create(new SKImageInfo(width, height));
        var owner = new SKColor(0xff000000 | MapArtCatalog.Stock.Factions[board.Identity.Observer.Value].Rgb);
        var samples = new List<object>();
        foreach (var center in new MapPoint[] { new(0, 4), new(0, 0), new(13, 9) })
            foreach (bool flat in new[] { true, false })
            {
                var observed = map with { Flat = flat, Cities = new([city with { Position = center }]) };
                var scene = new MapScene(observed);
                var radius = Enumerable.Range(0, rows).SelectMany(y => Enumerable.Range(0, columns).Select(x => new MapPoint(2 * x + (y & 1), y)))
                    .Where(point => Workable(point, center, flat)).ToHashSet();
                int size = radius.Count;
                Require((center == new MapPoint(0, 4) ? flat ? 12 : 21 : flat ? 7 : 12) == size,
                    "W01 gives the expected number of workable cells at the edge and corner");
                for (int y = 0; y < rows; y++)
                    for (int x = y & 1; x < observed.Width; x += 2)
                    {
                        var position = new MapPoint(x, y);
                        Require(scene.InCityRadius(position) == radius.Contains(position), "renderer radius matches the independent W01 lattice");
                    }
                var bare = new MapDisplay(Bare: true);
                long epoch = painter.SetScene(scene);
                painter.Draw(actual.Canvas, epoch, camera, width, height, 1, null, bare);
                using var ground = actual.Snapshot();
                var referencePixels = ReadMapPixels(actual);
                foreach (var (name, grid, cityRadius) in new[] { ("grid", true, false), ("radius", false, true), ("combined", true, true) })
                {
                    painter.Draw(actual.Canvas, epoch, camera, width, height, 1, null, bare with { Grid = grid, CityRadius = cityRadius });
                    reference.Canvas.DrawImage(ground, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
                    int first = flat ? 0 : -4, end = flat ? observed.Width : 20;
                    for (int y = 0; y < rows; y++)
                        for (int x = first + (y & 1); x < end; x += 2)
                        {
                            var point = new MapPoint((x + observed.Width) % observed.Width, y);
                            bool nearby = cityRadius && radius.Contains(point);
                            if (!grid && !nearby) continue;
                            PaintObject(reference.Canvas, textures, nearby ? "grid/radius" : "grid",
                                (int)camera.ScreenX(x) - 32, (int)camera.ScreenY(y) - 16, nearby ? owner : null);
                        }
                    var pixels = ReadMapPixels(actual);
                    int difference = DifferentMapPixels(pixels, ReadMapPixels(reference));
                    int changed = DifferentMapPixels(referencePixels, pixels);
                    Require(difference == 0 && changed > 0, $"independent {name} overlay at {center}, flat={flat} differs at {difference} pixels");
                    string filename = $"MV19-{center.X}-{center.Y}-{(flat ? "flat" : "wrapped")}-{name}.png";
                    if (name == "combined") Encode(actual, Path.Combine(output, filename));
                    samples.Add(new { center, flat, name, size, changed, image = Pixels(actual), filename = name == "combined" ? filename : null });
                }
            }

        var resource = new MapPoint(4, 4);
        var pollution = new MapPoint(8, 4);
        var secret = new MapPoint(12, 4);
        var composition = terrain.ToArray();
        composition[At(resource)] = plains with
        {
            Resource = 2,
            Improvements = MapImprovements.Road | MapImprovements.Irrigation | MapImprovements.Mine,
            Hut = MapFact<MapPresence>.Known(MapPresence.Present)
        };
        composition[At(pollution)] = plains with
        {
            Improvements = MapImprovements.Pollution | MapImprovements.Railroad,
            Ownership = MapFact<MapTileOwnership>.Known(new(new(3)))
        };
        composition[At(secret)] = default;
        var featureMap = map with { Flat = true, Tiles = new(composition), Cities = new([city with { Position = new(2, 6) }]) };
        var featureScene = new MapScene(featureMap);
        var selection = new MapPick(featureMap.Cities[0].Position, MapPickKind.City);
        long featureEpoch = painter.SetScene(featureScene);
        var full = painter.Draw(actual.Canvas, featureEpoch, camera, width, height, 1, selection);
        string fullImage = Pixels(actual);
        var bareDraw = painter.Draw(actual.Canvas, featureEpoch, camera, width, height, 1, null, new(Bare: true));
        var plan = PlanTerrainReference(featureMap, camera, width);
        plan.Add(new(TerrainPass.Resource, "resource/3", resource));
        plan.Add(new(TerrainPass.Pollution, "improvement/pollution", pollution));
        DrawTerrainReference(reference.Canvas, textures, plan, camera);
        var barePixels = ReadMapPixels(actual);
        Require(DifferentMapPixels(barePixels, ReadMapPixels(reference)) == 0 && full.Objects == 1 && bareDraw.Objects == 0,
            "bare terrain keeps only the supporting map, resources and pollution");
        painter.Draw(actual.Canvas, featureEpoch, camera, width, height, 1, null, new(Bare: true, Ownership: true));
        var owned = ReadMapPixels(actual);
        Require(CountPixelChanges(barePixels, owned, width, CenterBox(pollution)) > 0 &&
            CountPixelChanges(barePixels, owned, width, CenterBox(resource)) == 0 &&
            CountPixelChanges(barePixels, owned, width, CenterBox(secret)) == 0 &&
            DifferentMapPixels(barePixels, owned) == CountPixelChanges(barePixels, owned, width, TileBox(pollution)),
            "ownership tint touches only the admitted tile and reveals no territorial rule");
        Encode(actual, Path.Combine(output, "MV19-bare-ownership.png"));
        painter.Draw(actual.Canvas, featureEpoch, camera, width, height, 1, selection);
        Require(Pixels(actual) == fullImage && featureScene.PermitsPick(selection), "full mode restores the same scene and selection");
        File.WriteAllText(Path.Combine(output, "MV19-display-modes.json"), JsonSerializer.Serialize(new
        {
            map.Identity,
            samples,
            fullImage,
            stockBareOwnership = FileHash(Path.Combine(output, "MV19-bare-ownership.png"))
        }, Json));

        static bool Workable(MapPoint point, MapPoint city, bool flat)
        {
            int dx = Math.Abs(point.X - city.X);
            if (!flat) dx = Math.Min(dx, columns * 2 - dx);
            return Math.Abs(point.Y - city.Y) switch { 0 or 2 => dx <= 2, 1 => dx <= 3, 3 => dx <= 1, _ => false };
        }

        static int At(MapPoint point) => point.Y * columns + point.X / 2;
        SKRectI TileBox(MapPoint point) => CenterBox(point, 33, 17);
        SKRectI CenterBox(MapPoint point, int halfWidth = 5, int halfHeight = 5)
        {
            int x = (int)camera.ScreenX(point.X), y = (int)camera.ScreenY(point.Y);
            return new(x - halfWidth, y - halfHeight, x + halfWidth, y + halfHeight);
        }
    }

    private static int CountPixelChanges(uint[] before, uint[] after, int width, SKRectI rectangle)
    {
        int count = 0;
        for (int y = rectangle.Top; y < rectangle.Bottom; y++)
            for (int x = rectangle.Left; x < rectangle.Right; x++)
                if (before[y * width + x] != after[y * width + x]) count++;
        return count;
    }
}
