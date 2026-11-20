using System.Text.Json;
using OpenCiv2.App.Map;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

/// <summary>P02/MV14/MV16/MV25/MV29 authored presentation regressions. The same
/// cases run on CPU and GPU, including retained-to-fresh pixel equality.</summary>
internal static class MapPresentationChecks
{
    internal static void Run(AssetAtlas atlas, PinnedFonts fonts, string output, GRContext? context = null)
    {
        Directory.CreateDirectory(output);
        const int width = 160, height = 112;
        string backend = context is null ? "cpu" : "gpu";
        var rows = new List<object>();
        var city = MapGallery.Open("MV15").Map.Cities[0] with { Position = default, Name = new([]) };
        city = city with { Details = city.Details! with { Population = MapFact<int>.Withheld, Disorder = MapFact<MapPresence>.Withheld } };
        var unit = MapGallery.Open("MV10").Map.Units[0] with { Position = default, Details = MapUnitDetails.Withheld };
        var ground = new ObservedTile(2, default, false, 0, false);
        var world = MapGallery.Open("MV10").Map with
        {
            Width = 2,
            Height = 1,
            Flat = true,
            Tiles = new([ground]),
            Units = new([]),
            Cities = new([]),
            Annotations = MapAnnotations.Stock
        };
        Require(default(MapDisplay).Sentry == SentryAppearance.Greyscale && new MapDisplay().Sentry == SentryAppearance.Greyscale,
            "both default forms choose detailed greyscale");
        foreach (int density in new[] { 1, 2 })
            foreach (double zoom in new[] { .625, 1, 1.25 })
            {
                var camera = new MapCamera(width / 2, height / 2, zoom);
                var info = new SKImageInfo(width * density, height * density, SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
                using var retained = new MapPainter(atlas, fonts);
                using var surface = Create();
                using var fresh = Create();
                surface.Canvas.Scale(density);
                fresh.Canvas.Scale(density);
                byte[]? emptyCity = null, occupiedCity = null;
                foreach (var (name, garrison) in new (string, MapFact<MapPresence>)[]
                {
                    ("absent", MapFact<MapPresence>.Known(MapPresence.Absent)),
                    ("present", MapFact<MapPresence>.Known(MapPresence.Present)),
                    ("withheld", MapFact<MapPresence>.Withheld), ("unavailable", default)
                })
                {
                    var placed = city with { Details = city.Details! with { Garrison = garrison } };
                    var map = world with { Tiles = new([ground with { Improvements = MapFeatures.CitySite }]), Cities = new([placed]) };
                    var bare = Render(map, "city-" + name);
                    var stacked = map with { Units = new([unit, unit with { Type = new(5) }]) };
                    Require(bare.SequenceEqual(Render(stacked, "city-" + name + "-garrison")),
                        "city garrisons add no unit body, shadow, shield or badges");
                    var scene = new MapScene(stacked);
                    Require(scene.Objects(default).Count == 2 && scene.PermitsPick(new(default, MapPickKind.Unit)) &&
                        MapInspection.Describe(scene, new(default, MapPickKind.Unit)).Contains("2 visible in stack", StringComparison.Ordinal),
                        "hidden bodies retain admitted inspection and command records");
                    Require(scene.Pick(camera, camera.X, camera.Y - 12 * zoom) == new MapPick(default, MapPickKind.City),
                        "city body wins pointer picking over its hidden garrison");
                    if (name == "absent") emptyCity = bare;
                    else if (name == "present") occupiedCity = bare;
                    else Require(bare.SequenceEqual(emptyCity!), "undisclosed occupation never fabricates a flag");
                }
                Require(!occupiedCity!.SequenceEqual(emptyCity!), "only disclosed present occupation adds a flag");
                var remembered = world with { Tiles = new([ground with { Improvements = MapFeatures.CitySite }]) };
                Require(Render(remembered, "remembered").SequenceEqual(Render(remembered with { Units = new([unit]) }, "remembered-garrison")),
                    "remembered city sites hide stationary units too");
                var airbase = world with { Tiles = new([ground with { Improvements = MapFeatures.CitySite | MapFeatures.Fortification }]) };
                Require(!Render(airbase, "empty-airbase").SequenceEqual(Render(airbase with { Units = new([unit]) }, "airbase-unit")),
                    "airbases retain their unit bodies");
                var sleeping = world with { Units = new([unit with { Details = unit.Details with { Order = MapFact<UnitOrder>.Known(UnitOrder.Sleeping) } }]) };
                var grey = Render(sleeping, "sleep-grey", new(Sentry: SentryAppearance.Greyscale));
                Require(grey.SequenceEqual(Render(sleeping, "sleep-default")) && grey.SequenceEqual(Render(sleeping, "sleep-new", new MapDisplay())),
                    "implicit and constructed display defaults render the greyscale body");
                Require(!grey.SequenceEqual(Render(sleeping, "sleep-silhouette", new(Sentry: SentryAppearance.Silhouette))),
                    "optional silhouette remains distinct");

                // Independently compose the published overlay over an unfortified
                // production frame. Ignore the separate order/status banner region.
                Render(world with { Units = new([unit]) }, "ordinary-unit");
                using var body = surface.Snapshot();
                using var expected = Create();
                expected.Canvas.DrawImage(body, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
                expected.Canvas.Scale(density);
                using var textures = new MapTextures(atlas)
                {
                    GpuPreparation = context is not null,
                    EncodedSources = context is not null
                };
                var overlay = textures.Get("improvement/fortified", zoom * density);
                float left = (float)(camera.X - 32 * zoom), top = (float)(camera.Y - 32 * zoom);
                expected.Canvas.DrawImage(overlay, SKRect.Create(left, top, (float)(64 * zoom), (float)(48 * zoom)),
                    new SKSamplingOptions(SKFilterMode.Linear));
                var fortified = world with { Units = new([unit with { Details = unit.Details with { Order = MapFact<UnitOrder>.Known(UnitOrder.Fortified) } }]) };
                var actual = Render(fortified, "fortified");
                var reference = Read(expected);
                var anchor = MapPainter.UnitBinding(unit).Shield;
                var status = SKRect.Create((left + anchor.X * (float)zoom - 2) * density,
                    (top + anchor.Y * (float)zoom - 2) * density, (32 * (float)zoom + 6) * density, (30 * (float)zoom + 6) * density);
                int checkedPixels = 0;
                for (int y = 0; y < info.Height; y++)
                    for (int x = 0; x < info.Width; x++)
                    {
                        if (status.Contains(x, y)) continue;
                        int offset = 4 * (y * info.Width + x);
                        for (int channel = 0; channel < 3; channel++)
                        {
                            int error = Math.Abs(actual[offset + channel] - reference[offset + channel]);
                            if (error > (context is null ? 0 : 2))
                            {
                                NativeMapMatrix.Encode(surface, Path.Combine(output, "fortified-actual.png"));
                                NativeMapMatrix.Encode(expected, Path.Combine(output, "fortified-reference.png"));
                            }
                            Require(error <= (context is null ? 0 : 2),
                                $"fortification covers the body at {x},{y}, zoom {zoom}, density {density}, channel {channel}: {actual[offset + channel]} vs {reference[offset + channel]}");
                        }
                        checkedPixels++;
                    }
                Require(checkedPixels > 0, "overlay checks include the body region");
                NativeMapMatrix.Encode(surface, Path.Combine(output, $"fortified-{backend}-{density}x-{zoom}.png"));

                SKSurface Create() => context is null ? SKSurface.Create(info) : SKSurface.Create(context, true, info);
                byte[] Read(SKSurface target)
                {
                    if (context is null) { using var pixels = target.PeekPixels(); return pixels.GetPixelSpan().ToArray(); }
                    using var bitmap = GpuMapMatrix.Read(target);
                    return bitmap.GetPixelSpan().ToArray();
                }
                MapDrawStats Draw(MapPainter painter, SKSurface target, MapScene scene, MapDisplay display)
                {
                    long epoch = painter.SetScene(scene);
                    return context is null ? painter.Draw(target.Canvas, epoch, camera, width, height, density, null, display)
                        : MapGpuComposition.Draw(painter, context, target.Canvas, epoch, camera, width, height, density, null, display);
                }
                byte[] Render(MapObservation map, string name, MapDisplay display = default)
                {
                    var scene = new MapScene(map);
                    var stats = Draw(retained, surface, scene, display);
                    using var freshPainter = new MapPainter(atlas, fonts);
                    var baseline = Draw(freshPainter, fresh, scene, display);
                    Require(stats.Error is null && baseline.Error is null, "presentation frame renders without error");
                    var actual = Read(surface);
                    Require(actual.SequenceEqual(Read(fresh)), "retained and fresh pixels agree for " + name);
                    rows.Add(new { name, density, zoom, stats.Objects, stats.Composition });
                    if (density == 1 && zoom == 1 && name is "city-absent-garrison" or "city-present-garrison" or "sleep-default")
                        NativeMapMatrix.Encode(surface, Path.Combine(output, name + "-" + backend + ".png"));
                    return actual;
                }
            }
        File.WriteAllText(Path.Combine(output, "presentation-" + backend + ".json"), JsonSerializer.Serialize(new { backend, rows }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Presentation checks: {backend}, {rows.Count} frames, exact retained/fresh equality");
    }

    private static void Require(bool condition, string message) => NativeMapMatrix.Require(condition, message);
}
