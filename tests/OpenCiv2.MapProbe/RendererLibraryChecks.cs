using OpenCiv2.App.Map;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

/// <summary>P02/MV29 regression checks for the public host drawing boundary. Pixel expectations
/// come from fresh CPU renders, not independent original-game compatibility evidence.</summary>
internal static class RendererLibraryChecks
{
    internal static void Run(AssetAtlas atlas, PinnedFonts fonts)
    {
        var scene = MapScene.Create(MapGallery.Open("MV09").Map);
        var camera = NativeMapMatrix.BoardCamera(scene.Map);
        using var expected = SKSurface.Create(new SKImageInfo(960, 540));
        using var actual = SKSurface.Create(new SKImageInfo(960, 540));
        using var reference = new MapPainter(atlas, fonts);
        long referenceEpoch = reference.SetScene(scene);
        reference.Draw(expected.Canvas, referenceEpoch, camera, 960, 540, 1, null);
        string pixels = NativeMapMatrix.Pixels(expected);
        foreach (var mode in Enum.GetValues<MapRendererMode>())
        {
            var options = new MapRendererOptions(mode);
            using var painter = new MapPainter(atlas, fonts, options: options);
            var renderer = new MapRendererSession(options);
            long epoch = painter.SetScene(scene);
            var result = renderer.Draw(painter, null, actual.Canvas, epoch, camera, 960, 540, 1, null);
            Require(result.Stats.Error is null && result.Stats.Composition == "cpu" &&
                result.Stats.RequestedRenderer == mode.ToString().ToLowerInvariant() &&
                (result.Stats.RendererFallback is not null) == (mode == MapRendererMode.Gpu),
                "public draw reports CPU or explicit no-context fallback");
            Require(result.StartedAt > 0 && result.Milliseconds >= 0 && NativeMapMatrix.Pixels(actual) == pixels,
                "public draw matches fresh CPU pixels");
            renderer.Retry();
            renderer.Draw(painter, null, actual.Canvas, epoch, camera, 960, 540, 1, null);
            Require(NativeMapMatrix.Pixels(actual) == pixels, "public retry preserves frame contents");

            long cleared = painter.SetScene(null);
            renderer.Draw(painter, null, actual.Canvas, epoch, camera, 960, 540, 1, null);
            string stale = NativeMapMatrix.Pixels(actual);
            renderer.Draw(painter, null, actual.Canvas, cleared, camera, 960, 540, 1, null);
            Require(stale == NativeMapMatrix.Pixels(actual) && stale != pixels,
                "public draw conceals a revoked observation and stale epoch");
            epoch = painter.SetScene(scene);
            renderer.Draw(painter, null, actual.Canvas, epoch, camera, 960, 540, 1, null);
            Require(NativeMapMatrix.Pixels(actual) == pixels, "public draw restores a fresh binding");
        }
    }

    private static void Require(bool condition, string message) => NativeMapMatrix.Require(condition, message);
}
