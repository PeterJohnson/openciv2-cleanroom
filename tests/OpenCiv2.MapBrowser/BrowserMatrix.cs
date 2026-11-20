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
    public static async Task<string> FreshFrame()
    {
        var gallery = SeamApp.CurrentGallery!;
        var panel = (Grid)gallery.Content!;
        var result = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var capture = new ReferenceCapture(result) { Width = 1, Height = 1, IsHitTestVisible = false };
        panel.Children.Add(capture);
        try { return Convert.ToBase64String(await result.Task); }
        finally { panel.Children.Remove(capture); }
    }

    private static byte[] RenderReference(GRContext context)
    {
        var map = SeamApp.CurrentGallery!.Map;
        var draw = map.LastDraw!;
        int width = (int)Math.Ceiling(draw.Viewport.Width * draw.Scaling);
        int height = (int)Math.Ceiling(draw.Viewport.Height * draw.Scaling);
        bool supplied = SeamApp.CurrentGallery.Snapshot()["suppliedArt"]!.GetValue<bool>();
        using var painter = new MapPainter(Content!.Atlas, Content.Fonts, supplied ? Content.Art : null,
            parallelForeground: false, packGroundTextures: false);
        using var surface = SKSurface.Create(context, true,
            new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Opaque))
            ?? throw new InvalidOperationException("Could not allocate WebGL reference surface");
        surface.Canvas.Scale((float)draw.Scaling);
        painter.Draw(surface.Canvas, painter.SetScene(map.Scene), draw.Camera,
            draw.Viewport.Width, draw.Viewport.Height, draw.Scaling, map.Selection, map.Display, draw.Presentation);
        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    // This test-only control obtains the production host's current Skia lease.
    // It paints no UI pixels and never retains the context beyond that lease.
    private sealed class ReferenceCapture(TaskCompletionSource<byte[]> result) : Control
    {
        public override void Render(DrawingContext context) => context.Custom(new CaptureOperation(result));
    }

    private sealed class CaptureOperation(TaskCompletionSource<byte[]> result) : ICustomDrawOperation
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
                result.SetResult(RenderReference(lease.GrContext
                    ?? throw new InvalidOperationException("Reference requires the production WebGL context")));
            }
            catch (Exception error) { result.SetException(error); }
        }
    }
}
