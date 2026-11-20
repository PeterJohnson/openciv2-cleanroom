using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using OpenCiv2.App;
using OpenCiv2.MapRendering;
using SkiaSharp;

internal static partial class Program
{
    [JSExport]
    public static async Task<string> RunMatrix()
    {
        using var client = new HttpClient { BaseAddress = new Uri(BaseUrl()) };
        foreach (var (index, prefix, destination) in new[]
        {
            ("content/art/index.json", "content/art/", "/art"),
            ("content/acceptance/index.json", "content/acceptance/", "/")
        })
        {
            var files = await BrowserContent.LoadFilesAsync(client, index, prefix);
            foreach (var (path, bytes) in files)
            {
                string target = Path.Combine(destination, path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, bytes);
            }
        }
        Directory.SetCurrentDirectory("/");
        NativeMapMatrix.Run(Content!.Atlas, Content.Fonts, "/results", "/art");
        return File.ReadAllText("/results/matrix.json");
    }

    [JSExport]
    public static string ResultFiles() => JsonSerializer.Serialize(Directory.GetFiles("/results", "*", SearchOption.AllDirectories));

    [JSExport]
    public static byte[] ResultFile(string path)
    {
        if (!Path.GetFullPath(path).StartsWith("/results/", StringComparison.Ordinal)) throw new ArgumentException("Not a result file");
        return File.ReadAllBytes(path);
    }

    // Read-only reference for the frame actually submitted by the production
    // control, including its admitted observation, camera and sampled effects.
    [JSExport]
    public static Task<string> FreshFrame() => CaptureReference(false);

    [JSExport]
    public static Task<string> FreshRendererFrame() => CaptureReference(true);

    private static async Task<string> CaptureReference(bool sameRenderer)
    {
        var gallery = SeamApp.CurrentGallery!;
        var panel = (Grid)gallery.Content!;
        var result = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var capture = new ReferenceCapture(result, sameRenderer) { Width = 1, Height = 1, IsHitTestVisible = false };
        panel.Children.Add(capture);
        try { return Convert.ToBase64String(await result.Task); }
        finally { panel.Children.Remove(capture); }
    }

    private static byte[] RenderReference(GRContext? context, bool sameRenderer)
    {
        var gallery = SeamApp.CurrentGallery!;
        var map = gallery.Map;
        var draw = map.LastDraw!;
        int width = (int)Math.Ceiling(draw.Viewport.Width * draw.Scaling);
        int height = (int)Math.Ceiling(draw.Viewport.Height * draw.Scaling);
        bool supplied = gallery.Snapshot()["suppliedArt"]!.GetValue<bool>();
        var options = new MapRendererOptions(sameRenderer && draw.Stats.Composition == "gpu" ? MapRendererMode.Gpu : MapRendererMode.Cpu);
        using var painter = new MapPainter(Content!.Atlas, Content.Fonts, supplied ? Content.Art : null,
            parallelForeground: false, packGroundTextures: false, options: options);
        // Match the host's physical target and map origin. Drawing at (0, 0)
        // can change GPU interpolation rounding even for the same camera.
        var origin = map.TranslatePoint(default, gallery) ?? default;
        int left = (int)Math.Round(origin.X * draw.Scaling);
        int top = (int)Math.Round(origin.Y * draw.Scaling);
        var info = new SKImageInfo((int)Math.Ceiling(gallery.Bounds.Width * draw.Scaling),
            (int)Math.Ceiling(gallery.Bounds.Height * draw.Scaling), SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
        using var surface = (context is null ? SKSurface.Create(info) : SKSurface.Create(context, true, info))
            ?? throw new InvalidOperationException("Could not allocate reference surface");
        surface.Canvas.Scale((float)draw.Scaling);
        surface.Canvas.Translate((float)origin.X, (float)origin.Y);
        surface.Canvas.ClipRect(SKRect.Create((float)draw.Viewport.Width, (float)draw.Viewport.Height));
        new MapRendererSession(options).Draw(painter, context, surface.Canvas, painter.SetScene(map.Scene), draw.Camera,
            draw.Viewport.Width, draw.Viewport.Height, draw.Scaling, map.Selection, map.Display, draw.Presentation);
        using var image = surface.Snapshot(new SKRectI(left, top, left + width, top + height));
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    // This test-only control obtains the production host's current Skia lease.
    // It paints no UI pixels and never retains the context beyond that lease.
    private sealed class ReferenceCapture(TaskCompletionSource<byte[]> result, bool sameRenderer) : Control
    {
        public override void Render(DrawingContext context) => context.Custom(new CaptureOperation(result, sameRenderer));
    }

    private sealed class CaptureOperation(TaskCompletionSource<byte[]> result, bool sameRenderer) : ICustomDrawOperation
    {
        public Rect Bounds => new(0, 0, 1, 1);
        public bool HitTest(Point point) => false;
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }
        public void Render(ImmediateDrawingContext context)
        {
            if (result.Task.IsCompleted) return;
            try
            {
                var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()
                    ?? throw new InvalidOperationException("Missing production Skia lease");
                using var lease = feature.Lease();
                result.SetResult(RenderReference(lease.GrContext, sameRenderer));
            }
            catch (Exception error) { result.SetException(error); }
        }
    }
}
