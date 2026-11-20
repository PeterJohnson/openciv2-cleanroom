using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static partial class NativeMapMatrix
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private sealed record PrivacyFrame(double Zoom, double Density, string Image, string CosmeticFrame, string Overview, string Tile, string City, string Unit);

    public static void Run(AssetAtlas atlas, PinnedFonts fonts, string output, string contentCases, MemoryStream? diagnostic = null)
    {
        RendererLibraryChecks.Run(atlas, fonts);
        MapPresentationChecks.Run(atlas, fonts, output);
        Directory.CreateDirectory(output);
        var catalog = MapArtCatalog.Stock;
        Require(catalog.Images.Count == 368 && catalog.Units.Count == 63 && catalog.Cities.Count == 48, "exhaustive stock catalog");
        catalog.Verify(atlas);
        using var painter = new MapPainter(atlas, fonts);
        using var surface = SKSurface.Create(new SKImageInfo(1920, 1080));
        var cases = new List<object>();
        var actualBindings = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in MapGallery.Cases)
        {
            var board = MapGallery.Open(id);
            var scene = new MapScene(board.Map);
            var sprites = new List<TileSprite>();
            foreach (var expected in board.TerrainArt)
            {
                MapTileArt.Select(sprites, scene, expected.Point, MapDetail.Full, false);
                Require(sprites.Any(p => p.Key == expected.Key), $"{id} {expected.Point} selects {expected.Key}");
                if (id == "MV04")
                    foreach (var detail in new[] { MapDetail.Reduced, MapDetail.Overview })
                    {
                        MapTileArt.Select(sprites, scene, expected.Point, detail, false);
                        Require(sprites.Any(p => p.Key == expected.Key), $"{id} {detail} preserves {expected.Key}");
                    }
                actualBindings.Add(expected.Key);
            }

            if (id == "MV10")
            {
                var keys = board.Map.Units.Select(MapPainter.UnitBinding).ToArray();
                for (int i = 0; i < 63; i++)
                    Require(keys[i].Body == $"unit/{i}" && keys[i].Shield == (i is >= 32 and <= 43 ? new ArtPoint(2, 0) : new ArtPoint(48, 4)),
                        $"unit body and shield registration {i}");
                Require(keys[63] == keys[62] && keys[63].Body == "unit/62", "separate barbarian leader selector");
                actualBindings.UnionWith(keys.Select(key => key.Body));
            }

            if (id == "MV15")
            {
                int[] appearances = board.Map.Cities.Select(t => t.Details!.Appearance.Value!.Value.StockIndex).ToArray();
                Require(appearances.SequenceEqual(Enumerable.Range(0, 48)), "all city slots and registration");
                Require(catalog.Cities.All(art => art.Population.X >= 0 && art.Population.Y >= 0), "population anchors");
                var ink = catalog.Factions.Select(faction => MapPainter.PopulationInk(new(faction.Rgb))).ToArray();
                Require(ink[0] == SKColors.White && ink.Skip(1).All(color => color == SKColors.Black),
                    "readable population text against all eight stock faction colors");
                Require(MapPainter.PopulationInk(new(0xff00ff00)) == SKColors.Black &&
                    MapPainter.PopulationInk(new(0xff0000ff)) == SKColors.White, "population text also adapts to scenario colors");
                actualBindings.UnionWith(appearances.Select(i => catalog.Cities[i].Body));
            }
            var camera = BoardCamera(board.Map);
            long epoch = painter.SetScene(scene);
            var pick = board.Map.Units.Count > 0 ? new MapPick(board.Map.Units[0].Position, MapPickKind.Unit) : (MapPick?)null;
            VisualFrame? visual = id is "MV22" or "MV24" ? FeatureFrame(scene) : null;
            var stats = painter.Draw(surface.Canvas, epoch, camera, 1920, 1080, 1, pick, board.Display, visual);
            Require(stats.Tiles > 0 && stats.GroundChunks > 0 && stats.Batches > 0, $"{id} draws the production scene");
            string image = Path.Combine(output, id + ".png");
            Encode(surface, image);
            string fixture = Path.Combine(output, id + ".json");
            File.WriteAllText(fixture, JsonSerializer.Serialize(board, Json));
            cases.Add(new
            {
                id,
                board.Title,
                observer = board.Map.Identity,
                board.Display,
                zoom = camera.Zoom,
                detail = MapTileArt.Detail(camera.Zoom).ToString(),
                density = 1,
                fixture = Path.GetFileName(fixture),
                fixtureSha256 = FileHash(fixture),
                image = Path.GetFileName(image),
                imageSha256 = FileHash(image),
                selectors = board.TerrainArt.Count,
                stats.Tiles,
                stats.Objects,
                stats.Batches,
                stats.Labels
            });
            Console.WriteLine($"{id} {board.Title}: {board.TerrainArt.Count} selectors, {stats.Tiles} tiles, {stats.Objects} objects");
        }

        ContractChecks(painter, atlas, fonts, output);
        ClippingChecks(painter, output);
        TerrainBoundaryChecks(painter, output);
        ShorelinePixelChecks(painter, atlas, output);
        ConnectedFeaturePixels(painter, atlas, output);
        ScopedRouteMemoryChecks(atlas, fonts, output);
        ScopedPrivacyChecks(painter, output);
        RegionReplayChecks(atlas, fonts, output);
        WorkProjectionChecks(atlas, fonts, output);
        DisplayModePixelChecks(atlas, fonts, output);
        ScopedColorChecks(atlas, fonts, output);
        DetailAndPickingChecks(painter);
        DetailContinuityChecks(painter, output);
        LabelAndFallbackChecks(atlas, fonts, output);
        OcclusionPixelChecks(atlas, fonts, output);
        CachedTintChecks(atlas);
        UnitCompositionChecks(painter, output);
        UnitStatusPixelChecks(atlas, fonts, output);
        StationaryAircraftPixelChecks(atlas, fonts, output);
        RouteOverlayChecks(painter, output);
        FocusAndStackChecks(painter, output);
        OverviewFocusRasterChecks(atlas, fonts);
        WrappedEffectsChecks(painter, atlas, fonts, output);
        BackendRecoveryChecks(atlas, fonts, output);
        DetailedRecoveryChecks(atlas, fonts, output);
        // Single-thread WASM still executes the shared stale-generation and
        // replacement cases; only the native worker race requires threads.
        if (!OperatingSystem.IsBrowser()) ConcurrentSceneBindingChecks(atlas, fonts);
        FirstFrameBodyPreparationChecks(atlas, fonts);
        HigherBodyPreparationChecks(atlas, fonts);
        GroundBatchRecoveryChecks(atlas, fonts, output);
        SparseGroundDamageChecks(atlas, fonts);
        GroundCacheResizeChecks(atlas, fonts, output);
        SettledTerrainChecks(atlas, fonts);
        ViewportDamageChecks(atlas, fonts, output);
        ViewportScrollChecks(atlas, fonts, output);
        ForegroundBandFallbackChecks(atlas, fonts);
        LargeForegroundBandSeamChecks(atlas, fonts);
        SameTierBodyPreparationChecks(atlas, fonts);
        OverviewPixelsChecks(painter, output);
        var (native, replacement) = OverrideChecks(contentCases);
        StartupGroundPreparationChecks(atlas, fonts, native);
        LiveArtReplacementChecks(atlas, fonts, native, replacement, output);
        OwnerColorPixelChecks(atlas, fonts, null, output);
        OwnerColorPixelChecks(atlas, fonts, replacement, output);
        KnowledgeBoundaryPixels(atlas, fonts, null, output);
        KnowledgeBoundaryPixels(atlas, fonts, replacement, output);
        GroundNetworkPixelChecks(atlas, fonts, null, output);
        GroundNetworkPixelChecks(atlas, fonts, replacement, output);
        AgriculturalPixelChecks(atlas, fonts, null, output);
        AgriculturalPixelChecks(atlas, fonts, replacement, output);
        LeftShieldDamageChecks(atlas, fonts, contentCases, output);
        PinnedTerrainPixels(atlas, fonts, null, output);
        PinnedTerrainPixels(atlas, fonts, replacement, output);
        PinnedImprovementPixels(atlas, fonts, null, output);
        PinnedImprovementPixels(atlas, fonts, replacement, output);
        foreach (var (theme, art) in new[] { ("stock", (MapArtSelection?)null), ("native", native), ("classic", replacement) })
        {
            UnitArtPixelChecks(atlas, fonts, art, theme, output);
            CityArtPixelChecks(atlas, fonts, art, theme, output);
        }
        using (var scenario = new MapPainter(atlas, fonts, replacement))
        {
            foreach (string id in new[] { "MV01", "MV03", "MV10", "MV15", "MV28" })
            {
                var board = MapGallery.Open(id);
                var camera = BoardCamera(board.Map);
                long epoch = scenario.SetScene(new(board.Map));
                scenario.Draw(surface.Canvas, epoch, camera, 1920, 1080, 1, null);
                string image = Path.Combine(output, id + "-replacement.png");
                Encode(surface, image);
                Require(FileHash(image) != FileHash(Path.Combine(output, id + ".png")), $"scenario replacement presented in {id}");
            }
        }

        var identity = JsonDocument.Parse(ApplicationIdentity.Read(typeof(NativeMapMatrix).Assembly)).RootElement;
        string[] contractNames = ["docs/algorithms/P02-map-rendering-and-minimap.md", "docs/testing/map-rendering.md",
            "docs/performance/map-rendering.md", "docs/content/map-asset-bindings.json", "tools/dotnet/build_map_art_cases.py"];
        File.WriteAllText(Path.Combine(output, "matrix.json"), JsonSerializer.Serialize(new
        {
            format = "openciv2-native-map-matrix/1",
            backend = OperatingSystem.IsBrowser()
                ? "WASM Skia CPU raster used by production map painter; browser WebGL presentation is tested separately"
                : "Skia CPU raster used by production map painter; offscreen, no physical presentation evidence",
            workerConcurrency = OperatingSystem.IsBrowser() ? "single-thread WASM host; native worker race not applicable" : "native worker race executed",
            imageComparisonTolerance = 0,
            contentCases = replacement.Sources.ToArray(),
            contentDiagnostics = replacement.Diagnostics.ToArray(),
            identity,
            contracts = contractNames.ToDictionary(p => p, FileHash),
            cases,
            selectorIdentities = actualBindings.Order().ToArray(),
            featureCrossing = new { fixture = "MV04-pinned-crossing.json", sha256 = FileHash(Path.Combine(output, "MV04-pinned-crossing.json")) },
            shorelines = new { fixture = "MV02-shoreline-pixels.json", sha256 = FileHash(Path.Combine(output, "MV02-shoreline-pixels.json")) },
            groundNetworks = new[] { "MV03-MV04-stock.json", "MV03-MV04-classic.json" }
                .ToDictionary(fixture => fixture, fixture => FileHash(Path.Combine(output, fixture))),
            agriculture = new[] { "MV06-agriculture-stock.json", "MV06-agriculture-classic.json" }
                .ToDictionary(fixture => fixture, fixture => FileHash(Path.Combine(output, fixture))),
            routeMemory = new { fixture = "MV07-route-memory.json", sha256 = FileHash(Path.Combine(output, "MV07-route-memory.json")) },
            knowledgeAndInspection = new[] { "MV09-fog-stock.json", "MV09-fog-classic.json", "MV09-region-replay.json", "MV21-work-projection.json" }
                .ToDictionary(fixture => fixture, fixture => FileHash(Path.Combine(output, fixture))),
            colorAndDisplay = new[] { "MV17-role-stock.json", "MV17-role-classic.json", "MV17-scoped-colors.json", "MV19-display-modes.json" }
                .ToDictionary(fixture => fixture, fixture => FileHash(Path.Combine(output, fixture))),
            groundResize = new { fixture = "MV29-ground-resize.json", sha256 = FileHash(Path.Combine(output, "MV29-ground-resize.json")) },
            pinnedTerrain = new[] { "MV01-MV05-stock-pixels.json", "MV01-MV05-replacement-pixels.json" }
                .ToDictionary(fixture => fixture, fixture => FileHash(Path.Combine(output, fixture))),
            pinnedImprovements = new[] { "MV08-stock-pixels.json", "MV08-replacement-pixels.json" }
                .ToDictionary(fixture => fixture, fixture => FileHash(Path.Combine(output, fixture))),
            objectArt = new[] { "MV10-MV11-stock.json", "MV10-MV11-native.json", "MV10-MV11-classic.json",
                "MV15-MV16-stock.json", "MV15-MV16-native.json", "MV15-MV16-classic.json", "MV29-left-shield-dirty.json" }
                .ToDictionary(fixture => fixture, fixture => FileHash(Path.Combine(output, fixture))),
            unitStatus = new { fixture = "MV12-MV13-unit-status.json", sha256 = FileHash(Path.Combine(output, "MV12-MV13-unit-status.json")) },
            aircraft = new { fixture = "MV14-aircraft.json", sha256 = FileHash(Path.Combine(output, "MV14-aircraft.json")) },
            detailContinuity = new { fixture = "MV26-cutoffs.json", sha256 = FileHash(Path.Combine(output, "MV26-cutoffs.json")) },
            labels = new { fixture = "MV27-labels.json", sha256 = FileHash(Path.Combine(output, "MV27-labels.json")) },
            occlusion = new { fixture = "MV28-occlusion.json", sha256 = FileHash(Path.Combine(output, "MV28-occlusion.json")) },
            replacement = new { fixture = "MV29-live-themes.json", sha256 = FileHash(Path.Combine(output, "MV29-live-themes.json")) },
            recovery = new { fixture = "MV30-full-recovery.json", sha256 = FileHash(Path.Combine(output, "MV30-full-recovery.json")) },
            companionChecks = new[] { "style and health boundaries", "flat and seam-split overview placement", "HUD and effect life cycle",
                "privacy and path eligibility", "MV09-region-overhang.png", "MV22-region-hud.png", "MV09-hidden-invariance.json", "MV02–MV07 edge and mode selectors",
                "dirty and cached camera fresh comparisons", "resource eviction and density", "pinned river, road, rail and resource crossing pixels",
                "route colors, gaps, changed targets and wrapped pixels", "moving focus, retained stack and reduced-motion restoration",
                "whole-pixel terrain scroll, foreground recomposition, and exact flat/wrapped bounds at 1× and 2×",
                "cached masks retain source alpha, straight tint, and immutable source pixels",
                "MV30-chunk-batch-recovered.png" }
        }, Json));
        Console.WriteLine($"Native matrix rendered {cases.Count} boards, including {actualBindings.Count} explicitly asserted sprite identities.");
    }

    private static void ConnectedFeaturePixels(MapPainter painter, AssetAtlas atlas, string output)
    {
        const int columns = 5, rows = 9, width = 320, height = 208;
        var center = new MapPoint(4, 4);
        var northeast = new MapPoint(5, 3);
        var southwest = new MapPoint(3, 5);
        var east = new MapPoint(6, 4);
        var west = new MapPoint(2, 4);
        var plains = new ObservedTile(1, default, false, 0, false);
        var tiles = Enumerable.Repeat(plains, columns * rows).ToArray();
        Set(center, plains with { River = true, Resource = 2, Improvements = MapFeatures.Railroad });
        Set(northeast, plains with { River = true });
        Set(southwest, plains with { River = true });
        Set(east, plains with { Improvements = MapFeatures.Railroad });
        Set(west, plains with { Improvements = MapFeatures.Road });
        var map = MapGallery.Open("MV04").Map with { Width = columns * 2, Height = rows, Flat = true, Tiles = new(tiles) };
        var layers = new (MapPoint Position, string Key)[]
        {
            (northeast, "river/4"), (center, "river/5"), (southwest, "river/1"),
            (west, "road/2"), (center, "rail/2"), (center, "road/6"), (east, "rail/6"),
            (center, "resource/3")
        };
        using var textures = new MapTextures(atlas);
        using var actual = SKSurface.Create(new SKImageInfo(width, height));
        using var expected = SKSurface.Create(new SKImageInfo(width, height));
        using var omitted = SKSurface.Create(new SKImageInfo(width, height));
        var samples = new List<object>();
        foreach (double zoom in new double[] { 1, .5 })
        {
            var camera = new MapCamera(width / 2 - 4 * 32 * zoom, height / 2 - 4 * 16 * zoom, zoom);
            var scene = new MapScene(map);
            painter.Draw(actual.Canvas, painter.SetScene(scene), camera, width, height, 1, null);
            Draw(expected, null);
            int halfWidth = (int)(88 * zoom), halfHeight = (int)(25 * zoom);
            var region = new SKRectI(width / 2 - halfWidth, height / 2 - halfHeight,
                width / 2 + halfWidth, height / 2 + halfHeight);
            int mismatches = Different(actual, expected, region);
            if (mismatches != 0)
            {
                Encode(actual, Path.Combine(output, "crossing-actual.png"));
                Encode(expected, Path.Combine(output, "crossing-expected.png"));
            }
            Require(mismatches == 0, $"MV04/MV05/MV07 {zoom} pinned crossing comparison differs at {mismatches} pixels");
            var present = new Dictionary<string, int>();
            foreach (string feature in new[] { "river", "road", "rail", "resource" })
            {
                Draw(omitted, feature + "/");
                int changed = Different(actual, omitted, region);
                Require(changed > 0, $"MV04/MV05/MV07 {zoom} {feature} is visible in the combined frame");
                present.Add(feature, changed);
            }
            string image = zoom == 1 ? "MV04-pinned-crossing-full.png" : "MV04-pinned-crossing-reduced.png";
            Encode(actual, Path.Combine(output, image));
            samples.Add(new
            {
                zoom,
                region = new { region.Left, region.Top, region.Right, region.Bottom },
                visiblePixels = present,
                image,
                imageSha256 = FileHash(Path.Combine(output, image))
            });

            void Draw(SKSurface surface, string? omittedFamily)
            {
                surface.Canvas.Clear(new SKColor(0xff172b33));
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < columns; col++)
                        Sprite(new(2 * col + (row & 1), row), "terrain/1");
                foreach (var (position, key) in layers)
                    if (omittedFamily is null || !key.StartsWith(omittedFamily, StringComparison.Ordinal))
                        Sprite(position, key);

                void Sprite(MapPoint point, string key)
                {
                    var texture = textures.Get(key, zoom);
                    float x = (float)(camera.ScreenX(point.X) - 32 * zoom);
                    float y = (float)(camera.ScreenY(point.Y) - 16 * zoom);
                    float scale = (float)(64 * zoom / texture.Width);
                    surface.Canvas.DrawAtlas(texture, [SKRect.Create(texture.Width, texture.Height)], [new(scale, 0, x, y)],
                        new SKSamplingOptions(SKFilterMode.Linear));
                }
            }
        }
        File.WriteAllText(Path.Combine(output, "MV04-pinned-crossing.json"),
            JsonSerializer.Serialize(new { map, layers = layers.Select(layer => new { layer.Position, layer.Key }), samples }, Json));

        void Set(MapPoint point, ObservedTile tile) => tiles[point.Y * columns + point.X / 2] = tile;

        static int Different(SKSurface left, SKSurface right, SKRectI region)
        {
            using var a = left.PeekPixels();
            using var b = right.PeekPixels();
            int different = 0;
            for (int y = region.Top; y < region.Bottom; y++)
                for (int x = region.Left; x < region.Right; x++)
                    if (a.GetPixelColor(x, y) != b.GetPixelColor(x, y)) different++;
            return different;
        }
    }

    private static void OverviewPixelsChecks(MapPainter painter, string output)
    {
        const int columns = 6, rows = 8, width = 288, height = 128;
        var board = MapGallery.Open("MV10").Map;
        var unknown = new MapPoint(2, 2);
        var sea = new MapPoint(4, 2);
        var land = new MapPoint(6, 2);
        var cityPoint = new MapPoint(8, 2);
        var remembered = new MapPoint(10, 2);
        var odd = new MapPoint(3, 5);
        var city = MapGallery.Open("MV15").Map.Cities[3] with { Position = cityPoint };
        var tiles = Enumerable.Repeat(new ObservedTile(2, default, false, 0, false), columns * rows).ToArray();
        tiles[2 * columns + 1] = default;
        tiles[2 * columns + 2] = tiles[2 * columns + 2] with { Terrain = 10 };
        tiles[2 * columns + 5] = tiles[2 * columns + 5] with { Improvements = MapFeatures.CitySite };
        var map = board with
        {
            Width = 2 * columns,
            Height = rows,
            Flat = false,
            Tiles = new(tiles),
            Cities = new([city]),
            Units = new([board.Units[1] with { Position = land }])
        };
        using var outputImage = SKSurface.Create(new SKImageInfo(width, height));
        var camera = new MapCamera(64, -64, 1);
        var box = MapOverviewGeometry.Inner(map, width, height);
        var scene = new MapScene(map);
        Render(scene);
        Require(Pixel(unknown) == new SKColor(0xff293d42) && Pixel(sea) == new SKColor(0xff315b75) && Pixel(land) == new SKColor(0xff728862),
            "overview raster distinguishes unexplored, sea, and land with a unit still treated as land");
        Require(Pixel(remembered) == new SKColor(0xffb9a991) && MapOverviewGeometry.Describe(scene, remembered) == "Remembered city site",
            "an anonymous remembered city site keeps its separate type and color");
        Require(MapOverviewGeometry.Describe(scene, land) == "Land" && MapOverviewGeometry.Describe(scene, cityPoint) == "Civilization 3 city",
            "overview inspection offers admitted owner identity without disclosing unit details");
        string withUnit = Pixels(outputImage);
        Render(new(map with { Units = new([]) }));
        Require(Pixels(outputImage) == withUnit, "unit visibility does not affect any overview pixel");
        for (byte role = 0; role < 8; role++)
        {
            var roles = MapAnnotations.Stock.FactionRoles.ToArray();
            roles[city.Details!.Owner.Value] = role;
            Render(new(map with { Annotations = new(new(roles)) }));
            Require(Pixel(cityPoint) == new SKColor(0xff000000 | MapArtCatalog.Stock.Factions[role].Rgb), $"overview owner color role {role}");
        }
        foreach (var point in new[] { unknown, cityPoint, odd })
        {
            Render(scene, point);
            var painted = MapOverviewGeometry.CenterOf(map, box, point);
            Require(MapOverviewGeometry.Target(map, box, painted.X, painted.Y) == point &&
                Pixel(point) == new SKColor(0xff000000 | MapArtCatalog.Stock.DisplayColors["selectionHighlight"]),
                "overview marker and hit testing resolve the same cell center on both row parities");
        }
        Encode(outputImage, Path.Combine(output, "MV23-overview-pixels.png"));

        void Render(MapScene value, MapPoint? pick = null) => painter.DrawOverview(outputImage.Canvas, painter.SetScene(value),
            width, height, camera, 150, 88, pick);

        SKColor Pixel(MapPoint point)
        {
            var center = MapOverviewGeometry.CenterOf(map, box, point);
            using var pixels = outputImage.PeekPixels();
            return pixels.GetPixelColor((int)MathF.Round(center.X), (int)MathF.Round(center.Y));
        }
    }

    private static void ViewportDamageChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        var map = MapGallery.Open("MV10").Map;
        var city = MapGallery.Open("MV15").Map.Cities[1] with
        {
            Position = new(18, 14),
            Name = "A crowded river harbor with a very long name"
        };
        var neighbor = city with { Position = new(22, 14), Name = "East Harbor" };
        var unit = map.Units[1] with { Position = new(17, 15) };
        map = map with { Cities = new([city, neighbor]), Units = new([unit]) };
        var camera = new MapCamera(-150.25, 60.375, .83);
        const int width = 1280, height = 720;
        using var incremental = new MapPainter(atlas, fonts);
        using var fresh = new MapPainter(atlas, fonts);
        foreach (int density in new[] { 1, 2 })
        {
            using var target = SKSurface.Create(new SKImageInfo(width * density, height * density));
            using var baseline = SKSurface.Create(new SKImageInfo(width * density, height * density));
            target.Canvas.Scale(density);
            baseline.Canvas.Scale(density);
            incremental.SetScene(null);
            var initial = Frame(map, MapRedraw.Full);
            Require(initial.Labels == 1, "long label displaces its neighbor before the update");

            var renamed = city with { Name = "Port" };
            var revised = map with { Revision = 1, Cities = new([renamed, neighbor]) };
            var labels = Frame(revised, MapRedraw.Dirty);
            Require(labels.Labels == 2 && labels.DamagePixels > 0 && labels.PreparedChunks == 0, "renaming reflows labels without rebuilding terrain");

            var tiles = revised.Tiles.ToArray();
            int index = 14 * (map.Width / 2) + 9;
            tiles[index] = tiles[index] with { Terrain = 4, River = true, Improvements = MapFeatures.Road };
            tiles[index + 1] = tiles[index + 1] with { Improvements = MapFeatures.Road };
            var state = renamed.Details!;
            var appearance = state.Appearance.Value!.Value;
            var changed = revised with
            {
                Revision = 2,
                Tiles = new(tiles),
                Cities = new([renamed with { Details = state with
                {
                    Appearance = MapFact<MapCityAppearance>.Known(appearance with { Walled = !appearance.Walled }),
                    Population = MapFact<int>.Known(41)
                } }, neighbor]),
                Units = new([unit with { Position = new(19, 15), Details = unit.Details with { Health = MapFact<MapHealth>.Known(new(30, 28)) } }])
            };
            var explosion = new MapEffect(1, MapEffectKind.Explosion, city.Position, city.Position, 130);
            var effects = new VisualFrame(new([new EffectFrame(explosion, .5, city.Position.X)]), new([]), new([]), false);
            // Submit two observations before rendering to exercise the damage
            // against the frame actually displayed, including its old labels.
            incremental.SetScene(new(changed));
            var world = changed with { Revision = 3, Cities = new([changed.Cities[0] with { Name = city.Name }, neighbor]) };
            var damaged = Frame(world, MapRedraw.Dirty, effects);
            Require(damaged.DamagePixels < width * height * density * density / 2 && damaged.PreparedChunks > 0 &&
                damaged.GroundChunks < initial.GroundChunks, "local world change bounds both chunk composition and viewport repaint");
            Encode(target, Path.Combine(output, $"MV29-local-damage-{density}x.png"));
            var settled = Frame(world with { Revision = 4 }, MapRedraw.Cached, effects);
            Require(settled.DamagePixels == 0, "revision without new display facts retains its composed raster");

            MapDrawStats Frame(MapObservation observation, MapRedraw redraw, VisualFrame? visual = null)
            {
                long epoch = incremental.SetScene(new(observation));
                var result = incremental.Draw(target.Canvas, epoch, camera, width, height, density, null, visual: visual);
                fresh.SetScene(null);
                epoch = fresh.SetScene(new(observation));
                fresh.Draw(baseline.Canvas, epoch, camera, width, height, density, null, visual: visual);
                Require(result.Redraw == redraw && Pixels(target) == Pixels(baseline), $"{density}× {redraw} matches an independent fresh viewport");
                return result;
            }
        }
    }

    private static void BackendRecoveryChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        const int width = 420, height = 300;
        var camera = new MapCamera(48, 30, .83);
        var map = MapGallery.Open("MV25").Map;
        var pick = new MapPick(new(4, 4), MapPickKind.Tile);
        int fault = 1;
        using var target = SKSurface.Create(new SKImageInfo(width, height));
        using var baseline = SKSurface.Create(new SKImageInfo(width, height));
        using var fresh = new MapPainter(atlas, fonts);
        using var candidate = new MapPainter(atlas, fonts, allocateRaster: info =>
            fault == 2 || fault == 1 && info.Width == width && info.Height == height ? null : SKSurface.Create(info));
        long expectedEpoch = fresh.SetScene(new(map)), epoch = candidate.SetScene(new(map));
        fresh.Draw(baseline.Canvas, expectedEpoch, camera, width, height, 1, pick);
        string expected = Pixels(baseline);
        var direct = candidate.Draw(target.Canvas, epoch, camera, width, height, 1, pick);
        Require(direct.Redraw == MapRedraw.Direct && Pixels(target) == expected,
            "denied viewport raster falls back to a pixel-equivalent direct map");

        candidate.RecoverResources();
        fault = 2;
        var failed = candidate.Draw(target.Canvas, epoch, camera, width, height, 1, pick);
        Require(failed.Redraw == MapRedraw.Error && failed.Error == "Could not allocate map ground chunk" &&
            candidate.TextureUsage == (0, 0) && candidate.GroundUsage == (0, 0), "failed ground allocation clears rasters and explains the error");
        Encode(target, Path.Combine(output, "MV30-raster-allocation-error.png"));
        fault = 0;
        Require(candidate.Draw(target.Canvas, epoch, camera, width, height, 1, pick).Redraw == MapRedraw.Error,
            "a failed renderer waits for recovery without allocating on every paint");
        candidate.RecoverResources();
        var restored = candidate.Draw(target.Canvas, epoch, camera, width, height, 1, pick);
        Require(restored.Redraw == MapRedraw.Full && restored.Error is null && Pixels(target) == expected,
            "resource retry reproduces the fresh view and selection");
    }

    private static void GroundBatchRecoveryChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        const int width = 960, height = 620;
        var camera = new MapCamera(48, 30, .83);
        var map = MapGallery.Open("MV29").Map;
        var allocated = new List<SKSurface>();
        bool deny = true;
        using var target = SKSurface.Create(new SKImageInfo(width, height));
        using var baseline = SKSurface.Create(new SKImageInfo(width, height));
        using var candidate = new MapPainter(atlas, fonts, allocateRaster: info =>
        {
            if (!deny || info.Width > 260 || info.Height > 260) return SKSurface.Create(info);
            if (allocated.Count == 3) return null;
            var surface = SKSurface.Create(info);
            allocated.Add(surface);
            return surface;
        });
        long epoch = candidate.SetScene(new(map));
        var failed = candidate.Draw(target.Canvas, epoch, camera, width, height, 1, null);
        Require(failed.Redraw == MapRedraw.Error && failed.Error == "Could not allocate map ground chunk" &&
            allocated.Count == 3 && allocated.All(surface => surface.Handle == IntPtr.Zero) &&
            candidate.GroundUsage == (0, 0) && candidate.TextureUsage == (0, 0) && candidate.ViewportBytes == 0,
            "a chunk allocation failure frees earlier scratch surfaces and leaves no retained frame");
        deny = false;
        candidate.RecoverResources();
        var recovered = candidate.Draw(target.Canvas, epoch, camera, width, height, 1, null);
        using var reference = new MapPainter(atlas, fonts, packGroundTextures: false);
        var clean = reference.Draw(baseline.Canvas, reference.SetScene(new(map)), camera, width, height, 1, null);
        bool hasWorkers = !OperatingSystem.IsBrowser() && Environment.ProcessorCount > 1;
        bool packsGround = hasWorkers || OperatingSystem.IsBrowser();
        bool aligned = hasWorkers && OperatingSystem.IsLinux() && System.Runtime.Intrinsics.X86.Sse2.IsSupported && BitConverter.IsLittleEndian;
        Require(recovered.Redraw == MapRedraw.Full && recovered.ParallelGroundChunks == clean.ParallelGroundChunks &&
            (recovered.ParallelGroundChunks > 0) == hasWorkers &&
            (recovered.AlignedGroundChunks > 0) == aligned && clean.AlignedGroundChunks == 0 &&
            (packsGround ? recovered.Batches < clean.Batches : recovered.Batches == clean.Batches) &&
            Pixels(target) == Pixels(baseline),
            "ground batch recovery reproduces an independently built map");
        Encode(target, Path.Combine(output, "MV30-chunk-batch-recovered.png"));

        camera = camera with { Zoom = .45 };
        var half = candidate.Draw(target.Canvas, epoch, camera, width, height, 1, null);
        clean = reference.Draw(baseline.Canvas, reference.SetScene(new(map)), camera, width, height, 1, null);
        bool matches = Pixels(target) == Pixels(baseline);
        if (!matches)
        {
            Encode(target, Path.Combine(output, "half-ground-actual.png"));
            Encode(baseline, Path.Combine(output, "half-ground-expected.png"));
        }
        Require(half.Error is null && half.GroundTier == .5 &&
            (half.AlignedGroundChunks > 0) == aligned && clean.AlignedGroundChunks == 0 &&
            matches, "half-size terrain matches the independent linear sampler");

        camera = camera with { Zoom = 1.5 };
        var doubleSize = candidate.Draw(target.Canvas, epoch, camera, width, height, 1, null);
        clean = reference.Draw(baseline.Canvas, reference.SetScene(new(map)), camera, width, height, 1, null);
        Require(doubleSize.Error is null && doubleSize.GroundTier == 2 && doubleSize.PreparedChunks > 0 &&
            (doubleSize.AlignedGroundChunks > 0) == aligned && clean.AlignedGroundChunks == 0 &&
            Pixels(target) == Pixels(baseline), "first zoom into the double-size terrain tier matches serial Skia pixels");

        const int stripWidth = 750, stripHeight = 250;
        using var strip = SKSurface.Create(new SKImageInfo(stripWidth, stripHeight));
        using var expectedStrip = SKSurface.Create(new SKImageInfo(stripWidth, stripHeight));
        var mixed = MapGallery.Open("Mixed").Map;
        var stripView = new MapCamera(-2448, -2340, 1.5);
        var smallBatch = candidate.Draw(strip.Canvas, candidate.SetScene(new(mixed)), stripView, stripWidth, stripHeight, 1, null);
        clean = reference.Draw(expectedStrip.Canvas, reference.SetScene(new(mixed)), stripView, stripWidth, stripHeight, 1, null);
        Require(smallBatch.Error is null && smallBatch.PreparedChunks == 3 &&
            smallBatch.AlignedGroundChunks == (aligned ? 3 : 0) && clean.AlignedGroundChunks == 0 &&
            Pixels(strip) == Pixels(expectedStrip), "a small double-size batch uses the aligned compositor and preserves fresh pixels");
    }

    private static void GroundCacheResizeChecks(AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        const long standard = 240L * 1024 * 1024;
        const long packedAtlasBytes = 4L * 1024 * 1024;
        const int largeWidth = 2560, largeHeight = 1440, smallWidth = 720, smallHeight = 420;
        using var candidate = new MapPainter(atlas, fonts);
        var map = MapGallery.Open("Mixed").Map;
        long epoch = candidate.SetScene(new(map));
        var receipts = new List<object>();
        long peak = 0, largeLimit = 0, packedBytes = 0;
        using (var large = SKSurface.Create(new SKImageInfo(largeWidth * 2, largeHeight * 2)))
        {
            large.Canvas.Scale(2);
            foreach (double zoom in new[] { .3, .34, .38, .57, .65 })
            {
                var camera = new MapCamera(largeWidth / 2 - 126 * 32 * zoom, largeHeight / 2 - 126 * 16 * zoom, zoom);
                var drawn = candidate.Draw(large.Canvas, epoch, camera, largeWidth, largeHeight, 2, null);
                Require(drawn.Error is null && drawn.GroundCacheBytes <= drawn.GroundCacheLimitBytes,
                    "5K ground cache stays inside its actual limit across detail and backing changes");
                largeLimit = drawn.GroundCacheLimitBytes;
                peak = Math.Max(peak, drawn.GroundCacheBytes);
                receipts.Add(new { zoom, drawn.GroundCacheBytes, drawn.GroundCacheLimitBytes });
            }

            const double dirtyZoom = .65;
            var focus = new MapPoint(126, 126);
            var settled = new MapCamera(largeWidth / 2 - focus.X * 32 * dirtyZoom,
                largeHeight / 2 - focus.Y * 16 * dirtyZoom, dirtyZoom);
            Require(candidate.PackedGroundBytes == 0, "zoom leaves the retained art slot empty");
            using var expected = SKSurface.Create(new SKImageInfo(largeWidth * 2, largeHeight * 2));
            expected.Canvas.Scale(2);
            using var serial = new MapPainter(atlas, fonts, parallelForeground: false, packGroundTextures: false);
            for (int revision = 0; revision < 2; revision++)
            {
                map = MapGallery.Changed(map, focus);
                epoch = candidate.SetScene(new(map));
                var updated = candidate.Draw(large.Canvas, epoch, settled, largeWidth, largeHeight, 2, null);
                serial.SetScene(null);
                serial.Draw(expected.Canvas, serial.SetScene(new(map)), settled, largeWidth, largeHeight, 2, null);
                packedBytes = candidate.PackedGroundBytes;
                Require(updated.Error is null && updated.PreparedChunks > 0 && Pixels(large) == Pixels(expected),
                    "repeated ground edits at a settled camera match fresh serial pixels");
                Require(packedBytes is 0 or packedAtlasBytes && updated.GroundCacheBytes <= updated.GroundCacheLimitBytes,
                    "resident packed art uses one image inside the ground cache limit");
            }
            if (Environment.ProcessorCount > 1 && System.Runtime.Intrinsics.X86.Sse2.IsSupported && BitConverter.IsLittleEndian)
                Require(packedBytes == packedAtlasBytes, "the second local edit retains the packed art backing");
        }
        Require(largeLimit <= 544L * 1024 * 1024 && (largeLimit < 352L * 1024 * 1024 || peak > standard),
            "a large resident cache holds multiple tiers within the 5K memory cap");
        using var small = SKSurface.Create(new SKImageInfo(smallWidth, smallHeight));
        using var reference = SKSurface.Create(new SKImageInfo(smallWidth, smallHeight));
        using var fresh = new MapPainter(atlas, fonts);
        var closeup = new MapCamera(45, 40, 1);
        var resized = candidate.Draw(small.Canvas, epoch, closeup, smallWidth, smallHeight, 1, null);
        fresh.Draw(reference.Canvas, fresh.SetScene(new(map)), closeup, smallWidth, smallHeight, 1, null);
        Require(resized.GroundCacheLimitBytes == standard && resized.GroundCacheBytes <= standard && candidate.PackedGroundBytes == 0 &&
            Pixels(small) == Pixels(reference),
            "viewport shrink trims the cache and preserves exact output");
        candidate.SetScene(null);
        Require(candidate.GroundUsage == (0, 0) && candidate.ViewportBytes == 0,
            "removing the observation releases all resident ground and viewport rasters");
        File.WriteAllText(Path.Combine(output, "MV29-ground-resize.json"), JsonSerializer.Serialize(new
        {
            largeWidth = largeWidth * 2,
            largeHeight = largeHeight * 2,
            smallWidth,
            smallHeight,
            peak,
            largeLimit,
            packedBytes,
            shrunkBytes = resized.GroundCacheBytes,
            shrunkLimit = resized.GroundCacheLimitBytes,
            receipts,
            afterUnbind = candidate.GroundUsage.PixelBytes
        }, Json));
    }

    private static void WrappedEffectsChecks(MapPainter painter, AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        var board = MapGallery.Open("MV24").Map;
        var from = new MapPoint(board.Width - 2, 26);
        var to = new MapPoint(0, 26);
        var unit = board.Units.First(u => u.Target is not null) with { Position = to };
        var world = board with
        {
            Units = new([unit]),
            Cities = new([]),
            Annotations = MapAnnotations.Stock with { ActiveUnit = unit.Target }
        };
        var scene = new MapScene(world);
        var finished = new List<(ulong, PresentationEnd)>();
        var timeline = new MapVisualTimeline((id, end) => finished.Add((id, end)));
        timeline.Bind(world.Identity);
        const int width = 360, height = 200;
        using var raster = SKSurface.Create(new SKImageInfo(width, height));
        using var reference = SKSurface.Create(new SKImageInfo(width, height));
        using var fresh = new MapPainter(atlas, fonts);
        var camera = new MapCamera(128 - board.Width * 32, 100 - 26 * 16, 1);
        long epoch = painter.SetScene(scene), initial = fresh.SetScene(new(world));
        var movement = new MapEffect(1, MapEffectKind.Move, from, to, 300, unit);
        Require(timeline.Queue(world.Identity, movement, 0), "wrapped movement queued for the owned target");
        Require(timeline.Queue(world.Identity, new MapEffect(2, MapEffectKind.Explosion, to, to, 200), 0), "overlapping effect queued");
        var mid = timeline.Sample(scene, 150, false);
        Require(mid.Moving.SequenceEqual(new[] { unit.Target!.Value }) && mid.Pulse &&
            mid.Effects.Count == 2 && mid.Effects[0].WrappedDestinationX == board.Width,
            "wrapped movement suppresses the settled body while both effects are active");
        var steady = VisualFrame.Empty with { Pulse = true };
        fresh.Draw(reference.Canvas, initial, camera, width, height, 1, null, visual: steady);
        string expected = Pixels(reference);
        painter.Draw(raster.Canvas, epoch, camera, width, height, 1, null, visual: mid);
        Require(Pixels(raster) != expected, "wrapped movement and explosion contribute to the displayed frame");
        Encode(raster, Path.Combine(output, "MV24-wrapped-overlap.png"));

        var settled = timeline.Sample(scene, 300, false);
        Require(finished.Count == 2 && finished.All(row => row.Item2 == PresentationEnd.Finished) &&
            settled.Moving.Count == 0 && settled.Effects.Count == 0, "both semantic completions happen once");
        painter.Draw(raster.Canvas, epoch, camera, width, height, 1, null, visual: settled);
        Require(Pixels(raster) == expected, "world seam, old overhangs and banner restore to the independently rendered settled frame");
        timeline.Sample(scene, 350, false);
        Require(!timeline.Cancel(1) && finished.Count == 2, "late cancellation does not repeat a completion");
        Require(timeline.Queue(world.Identity, movement with { Id = 3 }, 400), "second movement queued");
        timeline.Bind(world.Identity with { Observer = new(2) });
        Require(finished.Last() == (3UL, PresentationEnd.Interrupted) && timeline.Sample(scene, 450, false) == VisualFrame.Empty,
            "observer replacement interrupts and suppresses the old effect");
    }

    private static void UnitCompositionChecks(MapPainter painter, string output)
    {
        var board = MapGallery.Open("MV10").Map;
        var point = new MapPoint(4, 4);
        var first = board.Units[1] with { Position = point };
        const int width = 400, height = 260;
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var camera = new MapCamera(80, 64, 1);
        var map = board with { Units = new([first]), Cities = new([]) };
        var art = MapPainter.UnitBinding(first);
        var shieldX = (int)camera.ScreenX(point.X) - 32 + art.Shield.X;
        var shieldY = (int)camera.ScreenY(point.Y) - 32 + art.Shield.Y;

        foreach (var (damage, color) in new[] { (0, "healthGreen"), (12, "healthYellow"), (23, "healthRed") })
        {
            Draw(first with { Details = first.Details with { Health = MapFact<MapHealth>.Known(new(30, damage)) } });
            using var pixels = surface.PeekPixels();
            uint health = MapArtCatalog.Stock.DisplayColors[color];
            Require(pixels.GetPixelColor(shieldX + 1, shieldY + 3) == new SKColor(0xff000000 | health),
                $"bar pixel uses {color} for thirty hit points and damage {damage}");
        }

        int InspectBody(SKColor target, out int colors)
        {
            var shades = new HashSet<uint>();
            using var pixels = surface.PeekPixels();
            int minX = (int)camera.ScreenX(point.X) - 25, maxX = (int)camera.ScreenX(point.X) + 8;
            int minY = (int)camera.ScreenY(point.Y) - 31, maxY = (int)camera.ScreenY(point.Y) + 6;
            int matching = 0;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var pixel = pixels.GetPixelColor(x, y);
                    if (pixel == target) matching++;
                    if (Math.Abs(pixel.Red - pixel.Green) <= 1 && Math.Abs(pixel.Green - pixel.Blue) <= 1)
                        shades.Add((uint)pixel);
                }
            colors = shades.Count;
            return matching;
        }

        var pose = first with { Details = first.Details with { Order = MapFact<UnitOrder>.Known(UnitOrder.Sleeping) } };
        uint shade = MapArtCatalog.Stock.DisplayColors["sentrySilhouette"];
        var silhouette = new SKColor(0xff000000 | shade);
        Draw(pose, new(Sentry: SentryAppearance.Silhouette));
        int solid = InspectBody(silhouette, out _);
        Encode(surface, Path.Combine(output, "MV14-sentry-silhouette.png"));
        // The source-mask checks above verify every tint channel and alpha
        // exactly. Downsampling this authored body leaves five fully opaque
        // pixels on native Skia and two on WASM; that count is not a P02 rule.
        Require(solid > 0, "the sentry silhouette contains the exact neutral body color");
        Draw(pose, new(Sentry: SentryAppearance.Greyscale));
        Require(InspectBody(silhouette, out int shades) != solid && shades > 5,
            "detailed sleeping art uses different neutral luminances");
        Encode(surface, Path.Combine(output, "MV14-sentry-greyscale.png"));

        for (byte role = 0; role < 8; role++)
        {
            var roles = MapAnnotations.Stock.FactionRoles.ToArray();
            roles[first.Owner.Value] = role;
            Draw(pose, annotations: new(new(roles)));
            using var pixels = surface.PeekPixels();
            var expected = new SKColor(0xff000000 | MapArtCatalog.Stock.Factions[role].Rgb);
            int matches = 0;
            // The health/order badges cover the shield interior. Its top
            // edge stays visible regardless of those independent fields.
            for (int y = shieldY; y < shieldY + 2; y++)
                for (int x = shieldX + 2; x < shieldX + 10; x++)
                    if (pixels.GetPixelColor(x, y) == expected) matches++;
            Require(matches > 1, $"shield alpha mask shows the reassigned stock color role {role}");
        }

        void Draw(ObservedMapUnit unit, MapDisplay display = default, MapAnnotations? annotations = null)
        {
            long epoch = painter.SetScene(new(map with { Units = new([unit]), Annotations = annotations }));
            painter.Draw(surface.Canvas, epoch, camera, width, height, 1, null, display);
        }
    }

    private static void CachedTintChecks(AssetAtlas atlas)
    {
        using var textures = new MapTextures(atlas);
        bool clear = false, partial = false, solid = false;
        foreach (string key in new[] { "unit/1", MapArtCatalog.Stock.Shield, MapArtCatalog.Stock.Disorder })
            foreach (double density in new[] { 1d, 2d })
            {
                var mask = textures.Get(key, density);
                using var before = mask.PeekPixels();
                byte[] raw = before.GetPixelSpan().ToArray();
                foreach (var color in new[] { new SKColor(0xfff4eee1), new SKColor(0x9df4eee1) })
                {
                    var result = textures.Get(key, density, color);
                    using var after = result.PeekPixels();
                    Require(after.ColorType == SKColorType.Rgba8888 && after.AlphaType == SKAlphaType.Unpremul,
                        "an owned tint preserves straight RGBA through the Skia bitmap snapshot");
                    for (int y = 0; y < mask.Height; y++)
                        for (int x = 0; x < mask.Width; x++)
                        {
                            byte alpha = before.GetPixelColor(x, y).Alpha;
                            byte expected = (byte)((alpha * color.Alpha + 127) / 255);
                            var tinted = after.GetPixelColor(x, y);
                            Require(tinted.Alpha == expected && (expected == 0 || tinted.WithAlpha(255) == color.WithAlpha(255)),
                                $"cached {key} {density}× tint preserves its author alpha mask and tint opacity");
                            clear |= alpha == 0;
                            partial |= alpha is > 0 and < 255;
                            solid |= alpha == 255;
                        }
                    Require(ReferenceEquals(result, textures.Get(key, density, color)), "same mask, color and scale reuse the owned image");
                }
                Require(before.GetPixelSpan().SequenceEqual(raw), "tint preparation preserves the immutable source pixels");
            }
        Require(clear && partial && solid, "the stock-comparison corpus includes transparent, antialiased and solid pixels");
        textures.Clear();
        Require(textures.Count == 0 && textures.PixelBytes == 0, "cached tints are released with the pinned texture cache");
    }

    private static void DetailAndPickingChecks(MapPainter painter)
    {
        var map = MapGallery.Open("MV10").Map;
        var unitAt = new MapPoint(4, 4);
        var cityAt = new MapPoint(5, 5);
        var city = MapGallery.Open("MV15").Map.Cities[0] with { Position = cityAt };
        var unit = map.Units[0] with { Position = unitAt };
        var world = map with { Flat = false, Units = new([unit]), Cities = new([city]) };
        var scene = new MapScene(world);
        var camera = new MapCamera(50, 70, .63);
        double x = camera.ScreenX(unitAt.X) + 16 * camera.Zoom, y = camera.ScreenY(unitAt.Y) - 4 * camera.Zoom;
        var expected = new MapPick(cityAt, MapPickKind.City);
        Require(scene.Pick(camera, x, y) == expected, "a later city wins over an earlier unit in an overlapping hit region");
        Require(scene.Pick(camera, x + world.Width * 32 * camera.Zoom, y) == expected,
            "the same occlusion and target identity apply across wrapped copies");
        var stack = new MapScene(world with { Units = new([unit with { Position = cityAt }]) });
        Require(stack.Pick(camera, camera.ScreenX(cityAt.X) + 8 * camera.Zoom, camera.ScreenY(cityAt.Y) - 12 * camera.Zoom)
            == new MapPick(cityAt, MapPickKind.City), "hidden same-cell unit body does not intercept city picking");
        Require(scene.Pick(camera, x, y, terrainOnly: true)?.Kind == MapPickKind.Tile, "bare terrain bypasses object hit boxes");

        var land = new ObservedTile(2, default, false, 0, false);
        var small = map with { Width = 2, Height = 1, Tiles = new([land]), Units = new([]), Cities = new([]) };
        foreach (int density in new[] { 1, 2 })
        {
            using var output = SKSurface.Create(new SKImageInfo(120 * density, 80 * density));
            output.Canvas.Scale(density);
            var full = Frame(.55, land);
            var irrigated = land with { Improvements = MapFeatures.Irrigation };
            Require(Frame(.55, irrigated) != full, "irrigation is visible exactly at full detail");
            double below = Math.BitDecrement(.55);
            Require(Frame(below, irrigated) == Frame(below, land), "detail follows the unrounded displayed zoom at either DPI");

            string Frame(double zoom, ObservedTile tile)
            {
                long epoch = painter.SetScene(new(small with { Tiles = new([tile]) }));
                var stats = painter.Draw(output.Canvas, epoch, new(45, 36, zoom), 120, 80, density, null);
                Require(stats.GroundTier == density * .5 && stats.Tiles == 1, "detail transition and backing resolution are independent");
                return Pixels(output);
            }
        }
    }

    private static void DetailContinuityChecks(MapPainter painter, string output)
    {
        const int width = 320, height = 140, worldWidth = 20, worldHeight = 12;
        var unitAt = new MapPoint(4, 4);
        var cityAt = new MapPoint(10, 6);
        var oceanAt = new MapPoint(14, 2);
        var riverAt = new MapPoint(15, 3);
        var fogAt = new MapPoint(16, 8);
        var unknownAt = new MapPoint(17, 7);
        var unitPick = new MapPick(unitAt, MapPickKind.Unit);
        var cityPick = new MapPick(cityAt, MapPickKind.City);
        var source = MapGallery.Open("MV10").Map;
        var unit = source.Units[0] with
        {
            Position = unitAt,
            Details = MapUnitDetails.Withheld with
            {
                Health = MapFact<MapHealth>.Known(new(30, 0)),
                Order = MapFact<UnitOrder>.Known(UnitOrder.Road),
                Work = MapFact<int>.Known(9)
            }
        };
        var city = MapGallery.Open("MV15").Map.Cities[0] with { Position = cityAt };
        var plains = new ObservedTile(1, default, false, 0, false);
        var ocean = plains with { Terrain = 10 };
        var tiles = Enumerable.Repeat(plains, worldWidth / 2 * worldHeight).ToArray();
        tiles[Index(oceanAt)] = ocean;
        tiles[Index(riverAt)] = plains with { River = true };
        tiles[Index(unknownAt)] = default;
        var map = source with
        {
            Width = worldWidth,
            Height = worldHeight,
            Flat = true,
            Tiles = new(tiles),
            Units = new([unit]),
            Cities = new([city])
        };
        var noMouth = WithTile(riverAt, plains);
        var noShoreContact = WithTile(riverAt, ocean);
        var noFogEdge = WithTile(unknownAt, plains);
        var marker = MapPainter.UnitBinding(unit).Shield;
        var rows = new List<object>();
        foreach (int density in new[] { 1, 2 })
        {
            using var surface = SKSurface.Create(new SKImageInfo(width * density, height * density));
            surface.Canvas.Scale(density);
            foreach (var (name, zoom, detail) in new[]
            {
                ("below-reduced", Math.BitDecrement(.35), MapDetail.Overview),
                ("at-reduced", .35, MapDetail.Reduced),
                ("below-full", Math.BitDecrement(.55), MapDetail.Reduced),
                ("at-full", .55, MapDetail.Full)
            })
            {
                // Put the health badge on an integer logical origin to measure its
                // actual colors at identical displayed zoom across two backing scales.
                const int barX = 87, barY = 67;
                var centerX = barX - (marker.X - 31) * zoom;
                var centerY = barY - (marker.Y - 30) * zoom;
                var camera = new MapCamera(centerX - unitAt.X * 32 * zoom, centerY - unitAt.Y * 16 * zoom, zoom);
                var scene = new MapScene(map);
                Require(MapTileArt.Detail(zoom) == detail, $"declared detail at {name}");
                Require(scene.Pick(camera, centerX, centerY - 12 * zoom) == unitPick &&
                    scene.Pick(camera, camera.ScreenX(cityAt.X), camera.ScreenY(cityAt.Y) - 12 * zoom) == cityPick,
                    $"body and city keep their pick targets at {name}");
                var inspection = MapInspection.Describe(scene, unitPick);
                Require(inspection.Contains("Order Road", StringComparison.Ordinal) &&
                    inspection.Contains("Accumulated work 9", StringComparison.Ordinal) &&
                    inspection.Contains("Health 30/30", StringComparison.Ordinal), $"the admitted details remain inspectable at {name}");

                var main = Frame(map, unitPick);
                Require(main.Stats.Objects == 2 && main.Stats.Labels == (detail == MapDetail.Overview ? 0 : 1),
                    $"city and unit remain visible with the declared caption policy at {name}");
                string image = $"MV26-{name}-{density}x.png";
                Encode(surface, Path.Combine(output, image));
                Require(Frame(map).Hash != main.Hash && Frame(map with { Cities = new([]) }, unitPick).Hash != main.Hash &&
                    Frame(map with { Units = new([]) }, unitPick).Hash != main.Hash,
                    $"selection, city and unit each contribute pixels at {name}");
                byte[] features = ImagePixels(map);
                byte[] withoutMouth = ImagePixels(noMouth);
                int mouthPixels = DifferentInTile(features, withoutMouth, oceanAt);
                int shorePixels = DifferentInTile(withoutMouth, ImagePixels(noShoreContact), oceanAt);
                int fogPixels = DifferentInTile(features, ImagePixels(noFogEdge), fogAt);
                Require(mouthPixels > 0 && shorePixels > 0 && fogPixels > 0,
                    $"river mouth ({mouthPixels}), coast ({shorePixels}) and fog edge ({fogPixels}) each contribute pixels at {name}, {density}×");
                var hiddenStatus = unit with
                {
                    Details = MapUnitDetails.Withheld with { Health = unit.Details.Health }
                };
                Require((Frame(With(hiddenStatus), unitPick).Hash != main.Hash) == (detail == MapDetail.Full),
                    $"only full detail paints the admitted work and order badges at {name}");

                var sampled = new List<uint>();
                foreach (var (damage, band) in new[] { (0, "healthGreen"), (12, "healthYellow"), (23, "healthRed") })
                {
                    Frame(With(hiddenStatus with { Details = hiddenStatus.Details with { Health = MapFact<MapHealth>.Known(new(30, damage)) } }));
                    using var pixels = surface.PeekPixels();
                    var expected = new SKColor(0xff000000 | MapArtCatalog.Stock.DisplayColors[band]);
                    var color = pixels.GetPixelColor(barX * density, barY * density);
                    Require(color == expected, $"{band} remains visible at {name}, {density}×: {color}");
                    sampled.Add((uint)color);
                }
                Frame(With(hiddenStatus with { Details = MapUnitDetails.Withheld }));
                using (var pixels = surface.PeekPixels())
                {
                    uint withheld = (uint)pixels.GetPixelColor(barX * density, barY * density);
                    Require(!sampled.Contains(withheld), $"withheld health is visually distinct at {name}");
                    sampled.Add(withheld);
                }
                string? annotationImage = null;
                if (detail == MapDetail.Full)
                {
                    var known = unit.Details with { Work = MapFact<int>.Known(231) };
                    byte[] clear = AnnotationPixels(hiddenStatus.Details);
                    byte[] order = AnnotationPixels(known with { Work = MapFact<int>.Withheld });
                    byte[] work = AnnotationPixels(known with { Order = MapFact<UnitOrder>.Withheld });
                    byte[] both = AnnotationPixels(known);
                    byte[] veteran = AnnotationPixels(known with { Veteran = MapFact<MapPresence>.Known(MapPresence.Present) });
                    int orderPixels = 0, workPixels = 0, veteranPixels = 0;
                    for (int i = 0; i < clear.Length; i += 4)
                    {
                        var original = clear.AsSpan(i, 4);
                        var orderPixel = order.AsSpan(i, 4);
                        var workPixel = work.AsSpan(i, 4);
                        var combined = both.AsSpan(i, 4);
                        var withVeteran = veteran.AsSpan(i, 4);
                        bool hasOrder = !original.SequenceEqual(orderPixel);
                        bool hasWork = !original.SequenceEqual(workPixel);
                        if ((hasOrder && hasWork) || !combined.SequenceEqual(hasOrder ? orderPixel : workPixel))
                            throw new InvalidOperationException($"Combined work and order cover the same pixels at {density}×");
                        if (hasOrder) orderPixels++;
                        if (hasWork) workPixels++;
                        if (!combined.SequenceEqual(withVeteran))
                        {
                            Require(!hasOrder && !hasWork, $"veteran mark leaves other annotations clear at {density}×");
                            veteranPixels++;
                        }
                    }
                    Require(orderPixels > 20 && workPixels > 20 && veteranPixels > 0, $"all three annotations render at {density}×");
                    annotationImage = $"MV26-unit-badges-{density}x.png";
                    Encode(surface, Path.Combine(output, annotationImage));
                }
                rows.Add(new
                {
                    name,
                    zoom,
                    detail = detail.ToString(),
                    density,
                    image,
                    imageSha256 = FileHash(Path.Combine(output, image)),
                    inspection,
                    badgePixels = sampled,
                    mouthPixels,
                    shorePixels,
                    fogPixels,
                    annotationImage,
                    annotationSha256 = annotationImage is null ? null : FileHash(Path.Combine(output, annotationImage))
                });

                byte[] AnnotationPixels(MapUnitDetails details)
                    => ImagePixels(With(unit with { Details = details }));

                byte[] ImagePixels(MapObservation value)
                {
                    Frame(value);
                    using var pixels = surface.PeekPixels();
                    return pixels.GetPixelSpan().ToArray();
                }

                int DifferentInTile(byte[] previous, byte[] next, MapPoint point)
                {
                    int changed = 0;
                    double x = camera.ScreenX(point.X) * density, y = camera.ScreenY(point.Y) * density;
                    double radiusX = 32 * zoom * density, radiusY = 16 * zoom * density;
                    for (int row = Math.Max(0, (int)(y - radiusY)); row < Math.Min(height * density, y + radiusY); row++)
                        for (int column = Math.Max(0, (int)(x - radiusX)); column < Math.Min(width * density, x + radiusX); column++)
                        {
                            if (Math.Abs(column + .5 - x) / radiusX + Math.Abs(row + .5 - y) / radiusY >= .95) continue;
                            int pixel = 4 * (row * width * density + column);
                            if (!previous.AsSpan(pixel, 4).SequenceEqual(next.AsSpan(pixel, 4))) changed++;
                        }
                    return changed;
                }

                MapObservation With(ObservedMapUnit value) => map with { Units = new([value]) };
                (string Hash, MapDrawStats Stats) Frame(MapObservation value, MapPick? selection = null)
                {
                    long epoch = painter.SetScene(new(value));
                    var stats = painter.Draw(surface.Canvas, epoch, camera, width, height, density, selection);
                    return (Pixels(surface), stats);
                }
            }
        }
        File.WriteAllText(Path.Combine(output, "MV26-cutoffs.json"), JsonSerializer.Serialize(rows, Json));

        static int Index(MapPoint point) => point.Y * (worldWidth / 2) + point.X / 2;

        MapObservation WithTile(MapPoint point, ObservedTile value)
        {
            var replaced = tiles.ToArray();
            replaced[Index(point)] = value;
            return map with { Tiles = new(replaced) };
        }
    }

    private static void TerrainBoundaryChecks(MapPainter painter, string output)
    {
        var source = MapGallery.Open("MV04").Map;
        const int w = 8, h = 8;
        var land = new ObservedTile(1, default, false, 0, false);
        var road = land with { Improvements = MapFeatures.Road, River = true };
        var tiles = Enumerable.Repeat(land, w * h / 2).ToArray();
        var center = new MapPoint(0, 2);
        var match = new MapPoint(7, 3);
        var hidden = new MapPoint(1, 1);
        Put(center, road);
        Put(match, road);
        Put(hidden, road with { Terrain = null });
        var map = source with { Width = w, Height = h, Flat = false, Tiles = new(tiles) };
        var sprites = new List<TileSprite>();
        Check(map, center, MapDetail.Full, false, ["river/4", "road/5", "fog/0"], ["river/5", "road/1"]);
        Check(map with { Flat = true }, center, MapDetail.Full, false, ["river/0", "road/0", "fog/0"], ["river/4", "road/5"]);
        Check(map, center, MapDetail.Reduced, false, ["river/4", "road/5"], []);
        Check(map, center, MapDetail.Overview, false, ["river/4", "fog/0"], ["road/5"]);
        using var target = SKSurface.Create(new SKImageInfo(320, 240));
        var camera = new MapCamera(90, 65, .46);
        long epoch = painter.SetScene(new(map));
        painter.Draw(target.Canvas, epoch, camera, 320, 240, 1, null);
        Encode(target, Path.Combine(output, "MV04-reduced-wrap.png"));

        var sea = land with { Terrain = 10 };
        Array.Fill(tiles, sea);
        Put(new(7, 1), land);
        var coast = map with { Tiles = new(tiles) };
        Check(coast, center, MapDetail.Full, false, ["coast/0/1", "coast/3/4"], ["coast/0/0"]);
        Check(coast with { Flat = true }, center, MapDetail.Full, false, ["coast/0/0", "coast/3/0"], ["coast/0/1"]);

        Array.Fill(tiles, land);
        var city = new MapPoint(4, 4);
        var farmland = land with { Improvements = MapFeatures.Irrigation | MapFeatures.Mine };
        Put(city, road with { Improvements = MapFeatures.CitySite | MapFeatures.Road | MapFeatures.Pollution, Resource = 2 });
        Put(new(5, 5), farmland);
        var farm = map with { Flat = true, Tiles = new(tiles) };
        Check(farm, city, MapDetail.Full, false, ["improvement/farmland", "resource/3", "improvement/pollution"], ["improvement/mine"]);
        Check(farm, city, MapDetail.Overview, true, ["resource/3", "improvement/pollution"], ["improvement/farmland", "road/0"]);
        Require((farm.Tiles[4 * w / 2 + 2].Improvements & farmland.Improvements) == 0, "city underlay leaves observed components untouched");
        Put(new(5, 5), farmland with { Terrain = null });
        Check(farm with { Tiles = new(tiles) }, city, MapDetail.Full, false, ["fog/1"], ["improvement/farmland"]);

        void Put(MapPoint p, ObservedTile tile) => tiles[p.Y * w / 2 + p.X / 2] = tile;

        void Check(MapObservation observation, MapPoint p, MapDetail detail, bool bare, string[] expected, string[] forbidden)
        {
            MapTileArt.Select(sprites, new(observation), p, detail, bare);
            foreach (string key in expected)
                Require(sprites.Any(sprite => sprite.Key == key), $"{p} {detail} expects {key}");
            foreach (string key in forbidden)
                Require(!sprites.Any(sprite => sprite.Key == key), $"{p} {detail} excludes {key}");
        }
    }

    private static void ScopedPrivacyChecks(MapPainter painter, string output)
    {
        using var session = new SeamSession();
        var world = session.Runtime.World;
        var hidden = new MapPoint(26, 4);
        var city = new MapPoint(12, 8);
        var seen = new MapPoint(12, 6);
        var before = session.Player.ObserveMap()!;
        var first = Capture(before);
        world.Map.TileAt(new(hidden.X, hidden.Y)) = new() { Terrain = Terrain.Mountains, River = true, ResourceSuppressed = true, Features = MapFeatures.CitySite | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Railroad | MapFeatures.Fortification | MapFeatures.Pollution, Owner = 2 };
        world.Entities.Units.Storage[3].Type = new(59);
        world.Entities.Units.Storage[3].Counter = 217;
        world.Entities.Units.Storage[4].Damage = 123;
        world.Entities.Units.Storage[4].Orders = UnitOrder.AiGoTo;
        world.Entities.Cities.Storage[1].Name = "Unavailable capital";
        world.Entities.Cities.Storage[1].Size = 24;
        session.Runtime.Publish();
        var after = session.Player.ObserveMap()!;
        Require(after.Revision > before.Revision && after.Tiles.Equals(before.Tiles) && after.Units.Equals(before.Units) && after.Cities.Equals(before.Cities),
            "mutated hidden terrain, entities and orders leave scoped observations identical");
        var second = Capture(after);
        Require(first.SequenceEqual(second), "hidden changes do not enter fresh, cached, overview, inspection or transient output at any detail band");

        world.Entities.Units.Storage[0].Orders = UnitOrder.AiIdle;
        session.Runtime.Publish();
        var aiIdle = session.Player.ObserveMap()!;
        var idleFrames = Capture(aiIdle);
        world.Entities.Units.Storage[0].Orders = UnitOrder.AiGoTo;
        session.Runtime.Publish();
        var aiGoto = session.Player.ObserveMap()!;
        var current = aiGoto.Units.Single(u => u.Target?.Slot.Value == 0);
        var aiInspection = MapInspection.Describe(new(aiGoto), new(current.Position, MapPickKind.Unit), current.Target);
        Require(current.Details.Order.State == MapFactState.Withheld &&
            aiInspection.Contains("Order withheld", StringComparison.Ordinal) &&
            aiIdle.Units.Equals(aiGoto.Units) && idleFrames.SequenceEqual(Capture(aiGoto)),
            "own AI order encodings remain withheld in rendering and inspection");
        File.WriteAllText(Path.Combine(output, "MV09-hidden-invariance.json"), JsonSerializer.Serialize(new
        {
            before = before.Revision,
            after = after.Revision,
            before.Identity,
            evidence = second,
            ownAiOrder = new { before = aiIdle.Revision, after = aiGoto.Revision, inspection = aiInspection }
        }, Json));

        PrivacyFrame[] Capture(MapObservation observation)
        {
            var game = session.Runtime.GameplayRandom.Capture();
            var cosmetic = session.Runtime.PresentationRandom.Capture();
            var tiles = world.Map.Tiles.ToArray();
            var units = world.Entities.Units.Scanned.ToArray();
            var cities = world.Entities.Cities.Scanned.ToArray();
            var scene = new MapScene(observation);
            var actor = observation.Units.First(u => u.Target is not null);
            var destination = new MapPoint(4, 2);
            Require(scene.PermitsSegment(actor.Position, destination, out _), "authored movement uses revealed Riverbend tiles");
            long epoch = painter.SetScene(scene);
            var rows = new List<PrivacyFrame>();
            foreach (var (zoom, density) in new[] { (.31, 1d), (.4, 2d), (.83, 1d), (.83, 2d) })
            {
                const int width = 900, height = 520;
                using var frame = SKSurface.Create(new SKImageInfo((int)(width * density), (int)(height * density)));
                using var overview = SKSurface.Create(new SKImageInfo(260, 120));
                frame.Canvas.Scale((float)density);
                var camera = new MapCamera(40, 90, zoom);
                var timeline = new MapVisualTimeline();
                timeline.Bind(observation.Identity);
                timeline.Queue(observation.Identity, new MapEffect(1, MapEffectKind.Nuclear, hidden, hidden, 300), 0);
                var visual = timeline.Sample(scene, 90, false);
                Require(visual.Effects.Count == 0, "concealed outcome suppressed before rendering");
                var selection = new MapPick(city, MapPickKind.City);
                var display = new MapDisplay(Grid: true, Ownership: true, CityRadius: true);
                painter.Draw(frame.Canvas, epoch, camera, width, height, density, selection, display, visual);
                string image = Pixels(frame);
                var cached = painter.Draw(frame.Canvas, epoch, camera, width, height, density, selection, display, visual);
                Require(cached.Redraw == MapRedraw.Cached && Pixels(frame) == image, "scoped raster cache retains the same allowed pixels");
                Require(timeline.Queue(observation.Identity, new MapEffect(2, MapEffectKind.Move, actor.Position, destination, 300, actor), 0) &&
                    timeline.Queue(observation.Identity, new MapEffect(3, MapEffectKind.Explosion, destination, destination, 300), 0),
                    "cosmetic inputs are bound to the observed scope");
                var active = timeline.Sample(scene, 90, false);
                var animated = painter.Draw(frame.Canvas, epoch, camera, width, height, density, selection, display, active);
                string cosmeticFrame = Pixels(frame);
                Require(animated.Moving == 1 && animated.Effects == 2 && cosmeticFrame != image, "permitted effects contribute pixels");
                painter.DrawOverview(overview.Canvas, epoch, 260, 120, camera, width, height, selection.Position);
                rows.Add(new(zoom, density, image, cosmeticFrame, Pixels(overview), MapInspection.Describe(scene, new(hidden, MapPickKind.Tile)),
                    MapInspection.Describe(scene, selection), MapInspection.Describe(scene, new(seen, MapPickKind.Unit))));
            }
            Require(game == session.Runtime.GameplayRandom.Capture() && cosmetic == session.Runtime.PresentationRandom.Capture() &&
                world.Map.Tiles.SequenceEqual(tiles) && world.Entities.Units.Scanned.SequenceEqual(units) && world.Entities.Cities.Scanned.SequenceEqual(cities) &&
                session.Player.ObserveMap()?.Revision == observation.Revision,
                "main map, overview, inspection and cosmetics leave world and RNG unchanged");
            return rows.ToArray();
        }
    }

    private static void ClippingChecks(MapPainter painter, string output)
    {
        var board = MapGallery.Open("MV10");
        var unit = board.Map.Units[0];
        var point = unit.Position;
        var restricted = new MapScene(board.Map with
        {
            Annotations = board.Map.Annotations! with { Region = new(new([point])) }
        });
        var camera = new MapCamera(50, 20, 1);
        float centerX = (float)camera.ScreenX(point.X), centerY = (float)camera.ScreenY(point.Y);
        Require(restricted.Pick(camera, centerX, centerY)?.Kind == MapPickKind.Unit, "visible unit is pickable in explicit region");
        Require(restricted.Pick(camera, centerX, centerY - 24) is null, "clipped tall art cannot be picked outside region");
        var stalePick = new MapPick(board.Map.Units[1].Position, MapPickKind.Unit);
        Require(!restricted.PermitsPick(stalePick), "selection outside region is invalid");

        const int width = 360, height = 240;
        using var target = SKSurface.Create(new SKImageInfo(width, height));
        long epoch = painter.SetScene(restricted);
        var effect = new MapEffect(1, MapEffectKind.Nuclear, point, point, 1000);
        var visual = new VisualFrame(new([new EffectFrame(effect, .55, point.X)]), new([]), new([]), true);
        painter.Draw(target.Canvas, epoch, camera, width, height, 1, stalePick, visual: visual,
            inspected: new(point, MapPickKind.Tile));
        using (var pixels = target.PeekPixels())
        {
            var background = pixels.GetPixelColor(0, 0);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (Math.Abs(x - centerX) / 32 + Math.Abs(y - centerY) / 16 > 1.075f)
                        Require(pixels.GetPixelColor(x, y) == background, "overhang and effect stay inside region");
            Require(pixels.GetPixelColor((int)centerX, (int)centerY) != background, "in-region world remains visible");
        }
        Encode(target, Path.Combine(output, "MV09-region-overhang.png"));
        string beforeHud = Pixels(target);
        painter.Draw(target.Canvas, epoch, camera, width, height, 1, stalePick,
            visual: visual with { Notices = new(["Viewport notice above a constrained map"]) }, inspected: new(point, MapPickKind.Tile));
        Require(Pixels(target) != beforeHud, "viewport notice has an independent clip");
        Encode(target, Path.Combine(output, "MV22-region-hud.png"));

        var paths = MapGallery.Open("MV04").Map;
        var from = new MapPoint(4, 4);
        var to = new MapPoint(6, 4);
        Require(new MapScene(paths).PermitsSegment(from, to, out var unwrapped) && unwrapped == to.X, "fully disclosed axial path");
        var tiles = paths.Tiles.ToArray();
        tiles[3 * (paths.Width / 2) + 2] = default;
        Require(!new MapScene(paths with { Tiles = new(tiles) }).PermitsSegment(from, to, out _), "the exact W01 corner intersects an undisclosed tile");
        var wrapped = new MapScene(paths with { Flat = false });
        Require(wrapped.PermitsSegment(new(paths.Width - 2, 4), new(0, 4), out unwrapped) && unwrapped == paths.Width,
            "wrapped path traverses the nearest copy");
    }

    private static void ContractChecks(MapPainter painter, AssetAtlas atlas, PinnedFonts fonts, string output)
    {
        // P02 MV12: local severity thresholds are thirds; raw damage remains inspectable.
        Require(MapPainter.HealthBand(MapFact<MapHealth>.Known(new(30, 9))) == "healthGreen", "green edge");
        Require(MapPainter.HealthBand(MapFact<MapHealth>.Known(new(30, 10))) == "healthYellow", "green to yellow");
        Require(MapPainter.HealthBand(MapFact<MapHealth>.Known(new(30, 19))) == "healthYellow", "yellow edge");
        Require(MapPainter.HealthBand(MapFact<MapHealth>.Known(new(30, 20))) == "healthRed", "yellow to red");
        Require(new MapHealth(0, 0).Remaining == 10 && new MapHealth(10, 16).Remaining == -6 &&
            new MapHealth(10, 16, true).Remaining == 10, "health substitutions preserve source values");
        Require(MapPainter.HealthBand(MapFact<MapHealth>.Withheld) is null, "withheld health does not imply healthy");
        foreach (int size in Enumerable.Range(0, 23))
        {
            int[] classical = [4, 6, 8], industrial = [5, 8, 11], modern = [5, 11, 19];
            Require(MapCityAppearance.Resolve(1, size, false, false, false, false, false).SizeClass == classical.Count(t => size >= t),
                $"style 1 size {size}");
            var era = MapCityAppearance.Resolve(1, size, true, false, true, false, true);
            Require(era.Style == 4 && era.Walled && era.SizeClass == industrial.Count(t => size >= t), $"industrial size {size}");
            var advanced = MapCityAppearance.Resolve(1, size, true, true, true, true, false);
            Require(advanced.Style == 5 && advanced.SizeClass == Math.Min(3, 1 + modern.Count(t => size >= t)), $"modern Palace {size}");
        }

        var board = MapGallery.Open("MV24");
        var scene = new MapScene(board.Map);
        var timeline = new MapVisualTimeline();
        timeline.Bind(board.Map.Identity);
        var ended = new List<(ulong, PresentationEnd)>();
        timeline = new((id, end) => ended.Add((id, end)));
        timeline.Bind(scene.Map.Identity);
        Require(timeline.Queue(board.Map.Identity, new MapNotice(1, "First notice", 100), 0), "first notice admitted");
        Require(timeline.Queue(board.Map.Identity, new MapNotice(2, "Second notice", 250), 0), "second notice admitted");
        Require(!timeline.Queue(board.Map.Identity, new MapNotice(2, "Duplicate", 250), 0), "duplicate notice ignored");
        Require(timeline.Sample(scene, 110, false).Notices.Count == 1 && ended.Single() == (1UL, PresentationEnd.Finished), "notice expiry");
        Require(timeline.Cancel(2) && !timeline.Cancel(2), "cancel consumes a notice exactly once");
        var events = MapGallery.Effects(board.Map);
        Require(events.All(effect => timeline.Queue(board.Map.Identity, effect, 0)), "four concurrent effects");
        Require(timeline.Sample(scene, 35, false).Effects.Count == 4, "overlapping effects admitted");
        Require(timeline.Sample(scene, 40, true).Effects.Count == 0 && ended.Count(row => row.Item2 == PresentationEnd.Skipped) == 4, "reduced-motion completion");
        foreach (var (kind, frames) in new[] { (MapEffectKind.Explosion, 8), (MapEffectKind.Nuclear, 11) })
        {
            var frameDir = Path.Combine(output, kind.ToString());
            Directory.CreateDirectory(frameDir);
            using var target = SKSurface.Create(new SKImageInfo(640, 420));
            var hashes = new HashSet<string>();
            var p = new MapPoint(8, 8);
            var camera = MapCamera.Initial;
            long epoch = painter.SetScene(scene);
            for (int i = 0; i < frames; i++)
            {
                double progress = (i + 0.35) / frames;
                var effect = new MapEffect(1, kind, p, p, 1000);
                var visual = new VisualFrame(new([new EffectFrame(effect, progress, p.X)]), new([]), new([]), true);
                painter.Draw(target.Canvas, epoch, camera, 640, 420, 1, null, visual: visual);
                Require(MapVisualTimeline.FrameIndex(kind, progress) == i, $"exact {kind} frame {i}");
                hashes.Add(Pixels(target));
                Encode(target, Path.Combine(frameDir, $"{i:D2}.png"));
            }
            Require(hashes.Count == frames, $"distinct {kind} frames appear");
        }
        var movement = events[0];
        Require(timeline.Queue(board.Map.Identity, movement with { Id = 7 }, 0), "moving object admitted");
        var lost = new MapScene(board.Map with { Units = new(board.Map.Units.Where(u => movement.Unit?.Target != u.Target)) });
        timeline.Sample(lost, 40, false);
        Require(ended.Contains((7UL, PresentationEnd.Interrupted)), "deleted actor clears transient effect");
        timeline.Queue(board.Map.Identity, new MapNotice(8, "Old observer", 500), 0);
        timeline.Bind(board.Map.Identity with { Observer = new(2) });
        Require(ended.Contains((8UL, PresentationEnd.Interrupted)), "observer replacement clears notices");

        var map = board.Map;
        var inset = MapOverviewGeometry.Inner(map, 180, 96);
        var seam = new MapCamera(30, 0, 1);
        Require(MapOverviewGeometry.Footprint(map, inset, seam, 380, 300).Count() == 2, "split wrap footprint");
        Require(MapOverviewGeometry.Footprint(map with { Flat = true }, inset, seam, 380, 300).Count() == 1, "flat footprint clamped");
        Require(MapOverviewGeometry.Target(map, inset, inset.MidX, inset.MidY) is { } targetPoint && scene.Contains(targetPoint), "canonical overview click");

        using var changed = SKSurface.Create(new SKImageInfo(720, 520));
        using var direct = SKSurface.Create(new SKImageInfo(720, 520));
        using var reference = new MapPainter(atlas, fonts);
        var initialCamera = MapCamera.Initial;
        var cameraStep = initialCamera.Pan(-1, .25, map, 720, 520);
        long current = painter.SetScene(scene);
        painter.Draw(changed.Canvas, current, initialCamera, 720, 520, 1, null);
        var panned = painter.Draw(changed.Canvas, current, cameraStep, 720, 520, 1, null);
        long fresh = reference.SetScene(new(map));
        reference.Draw(direct.Canvas, fresh, cameraStep, 720, 520, 1, null);
        Require(panned.GroundChunks > 0 && panned.PreparedChunks < panned.GroundChunks && Pixels(changed) == Pixels(direct), "same pixels after cached pan uses retained world chunks");

        var revised = MapGallery.Changed(map);
        current = painter.SetScene(new(revised));
        fresh = reference.SetScene(null);
        fresh = reference.SetScene(new(revised));
        var dirty = painter.Draw(changed.Canvas, current, cameraStep, 720, 520, 1, null);
        reference.Draw(direct.Canvas, fresh, cameraStep, 720, 520, 1, null);
        Require(dirty.PreparedChunks > 0 && dirty.PreparedChunks <= dirty.GroundChunks && Pixels(changed) == Pixels(direct), "dirty world equals clean frame");
        string recovered = Pixels(changed);
        painter.RecoverResources();
        painter.Draw(changed.Canvas, current, cameraStep, 720, 520, 1, null);
        Require(Pixels(changed) == recovered, "cache eviction and recreation produce identical pixels");
        var selection = new MapPick(new(4, 4), MapPickKind.Tile);
        painter.Draw(changed.Canvas, current, cameraStep, 360, 260, 2, selection, new(Bare: true));
        reference.Draw(direct.Canvas, fresh, cameraStep, 360, 260, 2, selection, new(Bare: true));
        Require(Pixels(changed) == Pixels(direct), "density and bare terrain invalidate caches");

        var clipped = new MapScene(revised with { Annotations = revised.Annotations! with { Region = new(new([selection.Position])) } });
        Require(clipped.PermitsSegment(new(4, 4), new(6, 4), out _) == false, "clipped region suppresses non-admitted route");
        var layers = new List<TileSprite>();
        MapTileArt.Select(layers, clipped, new(6, 4), MapDetail.Full, false);
        Require(layers.Count == 1 && layers[0].Key == "fog/full", "clipped region draws no source terrain");
        foreach (double zoom in new[] { .25, .349, .35, .549, .55, 1, 1.4 })
            Require(MapTileArt.Detail(zoom) == (zoom >= .55 ? MapDetail.Full : zoom >= .35 ? MapDetail.Reduced : MapDetail.Overview),
                "detail bands use displayed scale");

        var roles = revised.Annotations!.FactionRoles.ToArray();
        roles[1] = 17;
        current = painter.SetScene(new(revised with { Annotations = revised.Annotations with { FactionRoles = new(roles) } }));
        var failed = painter.Draw(changed.Canvas, current, cameraStep, 720, 520, 1, null);
        Require(failed.Redraw == MapRedraw.Error && failed.Error == "Unsupported owner color role 17" && failed.Objects == 0,
            "an unbound color produces an explicit limited frame");
        Encode(changed, Path.Combine(output, "MV30-unsupported-color.png"));
        current = painter.SetScene(new(revised));
        var restored = painter.Draw(changed.Canvas, current, cameraStep, 720, 520, 1, null);
        Require(restored.Objects > 0 && restored.Error is null && Pixels(changed) == recovered,
            "replacing unsupported input restores the previously correct frame");
    }

    internal static (MapArtSelection Native, MapArtSelection Classic) OverrideChecks(string root)
    {
        static Func<string, Stream?> Leaf(string directory) => leaf =>
        {
            var path = Directory.EnumerateFiles(directory).SingleOrDefault(p => Path.GetFileName(p).Equals(leaf, StringComparison.OrdinalIgnoreCase));
            return path is null ? null : File.OpenRead(path);
        };

        string nativeDirectory = Path.Combine(root, "native");
        Func<string, Stream> native = path => File.OpenRead(Path.Combine(nativeDirectory, path));
        foreach (var (metadata, diagnostic) in new (string, string)[]
        {
            ("[]", "must be an object"),
            ("{}", "Missing native map art field 'format'"),
            ("""{"format":"openciv2-map-art/1","images":[]}""", "images must be a named object"),
            ("""{"format":"openciv2-map-art/1","images":{"terrain/2":{"path":null,"sha256":"unused"}}}""", "'path' must be a string"),
            ("""{"format":"openciv2-map-art/1","images":{"terrain/2":{"path":"unused","path":"unused","sha256":"unused"}}}""", "duplicate map art field 'path'")
        })
        {
            bool diagnosed = false;
            try { MapArtSelection.Load(_ => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(metadata))); }
            catch (InvalidDataException e) { diagnosed = e.Message.Contains(diagnostic, StringComparison.Ordinal); }
            Require(diagnosed, $"malformed map theme diagnoses {diagnostic}");
        }
        var nativeOnly = MapArtSelection.Load(native);
        Require(nativeOnly.Image("unit/32")?.Marker == new ArtPoint(22, 6) &&
            nativeOnly.Image("terrain/2")?.Pixels.Width == 64, "explicit native PNG and registration");
        var replacement = MapArtSelection.Load(native, Leaf(Path.Combine(root, "scenario")), Leaf(Path.Combine(root, "installation")));
        Require(replacement.Sources.Any(s => s.Source == "scenario/TERRAIN1.BMP"), "scenario BMP selected");
        Require(replacement.Sources.Any(s => s.Source == "scenario/UNITS.GIF") && replacement.Diagnostics.Any(s => s.Contains("UNITS.BMP", StringComparison.Ordinal)),
            "malformed BMP diagnoses and falls back to scenario GIF");
        Require(replacement.Sources.Any(s => s.Source == "installation/CITIES.GIF"), "installation GIF fallback");
        Require(replacement.Image("unit/62")?.Marker == new ArtPoint(17, 6), "scenario unit last border marker");
        Require(replacement.Image("city/47")?.Marker == new ArtPoint(15, 9) && replacement.Image("city/47")?.Flag == new ArtPoint(42, 7),
            "scenario city population and flag anchors");
        Require(Pixel(replacement.Image("unit/32")!.Pixels, 32, 24).A == 0 &&
            Pixel(replacement.Image("unit/32")!.Pixels, 32, 12).A == 255, "scenario keyed holes");
        Require(Pixel(replacement.Image("terrain/2")!.Pixels, 33, 16) != Pixel(nativeOnly.Image("terrain/2")!.Pixels, 33, 16),
            "classic scenario takes precedence over base native art");
        var grass = replacement.Image("terrain/2")!.Pixels;
        var edge = replacement.Image("dither/2/0")!.Pixels;
        var coastline = replacement.Image("dither/12/0")!.Pixels;
        Require(Pixel(edge, 0, 1) == Pixel(grass, 32, 1) && Pixel(edge, 0, 1).A == 255 && Pixel(edge, 0, 2).A == 0,
            "classic blend uses the selected neighboring terrain with keyed mask holes");
        Require(Pixel(coastline, 0, 1).A == 255 && Pixel(coastline, 0, 1) != Pixel(replacement.Image("terrain/10")!.Pixels, 32, 1),
            "Ocean blend uses its distinct declared source rectangle");
        Require(Pixel(replacement.Image("fog/full")!.Pixels, 32, 16).A == 255 &&
            Pixel(replacement.Image("fog/0")!.Pixels, 0, 2).A == 0, "selected hidden tile and clipped feather mask");
        Require(Pixel(replacement.Image(MapArtCatalog.Stock.Factions[0].OccupiedAirbase)!.Pixels, 18, 12) !=
            Pixel(replacement.Image(MapArtCatalog.Stock.Factions[7].OccupiedAirbase)!.Pixels, 18, 12), "occupied airbase tint replacement");
        return (nativeOnly, replacement);

        static (byte R, byte G, byte B, byte A) Pixel(RgbaImage src, int x, int y)
        {
            var bytes = new byte[src.Width * src.Height * 4];
            src.CopyPixels(bytes);
            int offset = (y * src.Width + x) * 4;
            return (bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]);
        }
    }

    internal static MapCamera BoardCamera(MapObservation map)
    {
        double zoom = Math.Clamp(Math.Min(1850d / (map.Width * 32), 1010d / (map.Height * 16)), .25, 2.5);
        return new(38, 25, zoom);
    }

    internal static VisualFrame FeatureFrame(MapScene scene)
    {
        var timeline = new MapVisualTimeline();
        timeline.Bind(scene.Map.Identity);
        timeline.Queue(scene.Map.Identity, new MapNotice(1, "Authored viewport notification", 1500), 0);
        timeline.Queue(scene.Map.Identity, new MapNotice(2, "Second eligible notification", 1500), 0);
        foreach (var item in MapGallery.Effects(scene.Map)) timeline.Queue(scene.Map.Identity, item, 0);
        return timeline.Sample(scene, 600, false);
    }

    internal static void Encode(SKSurface surface, string path)
    {
        using var image = surface.Snapshot();
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var output = File.Create(path);
        png.SaveTo(output);
    }

    internal static string Pixels(SKSurface surface)
    {
        using var pixels = surface.PeekPixels();
        return Convert.ToHexStringLower(SHA256.HashData(pixels.GetPixelSpan()));
    }

    internal static string FileHash(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(file));
    }

    internal static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Native map matrix: {label}");
    }
}
