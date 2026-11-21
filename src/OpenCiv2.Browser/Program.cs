using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Browser;
using OpenCiv2.App;
using OpenCiv2.MapRendering;

internal static partial class Program
{
    private static string? observed;
    internal static BrowserContent.Loaded? Content { get; private set; }

    [JSImport("baseUrl", "browser")]
    internal static partial string BaseUrl();

    [JSImport("option", "browser")]
    private static partial string? Option(string name);

    [JSExport]
    public static string Identity() => ApplicationIdentity.Read(typeof(Program).Assembly);

    [JSExport]
    public static string Snapshot() => observed ?? SeamApp.CurrentView?.Snapshot() ?? "{}";

    public static async Task Main()
    {
        var storage = new BrowserStorage();
        await SeamApp.ConfigureServicesAsync(storage, storage);
        var backend = Option("backend") switch
        {
            null or "webgl" => BrowserRenderingMode.WebGL2,
            "software" => BrowserRenderingMode.Software2D,
            _ => throw new ArgumentException("Browser backend must be software or webgl")
        };
        MapRendererOptions saved = new();
        string? warning = null;
        try
        {
            if (await storage.ReadAsync("renderer-preferences") is { } bytes)
            {
                saved = MapRendererOptions.Read(new MemoryStream(bytes));
            }
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
        { warning = "Could not load renderer preference: " + error.Message; }
        var renderer = new MapRendererOptions(MapRendererOptions.ParseMode(Option("renderer") ??
            (backend == BrowserRenderingMode.Software2D ? "cpu" : saved.Mode == MapRendererMode.Gpu ? "gpu" : "cpu")), saved.GpuCacheMiB);
        if (backend == BrowserRenderingMode.Software2D && renderer.Mode == MapRendererMode.Gpu)
        {
            throw new ArgumentException("GPU map composition requires the WebGL browser backend");
        }

        SeamApp.ConfigureMapRenderer(new(renderer, saved, warning: warning, saveAsync: async options =>
        {
            using var output = new MemoryStream();
            options.Write(output);
            await storage.WriteAsync("renderer-preferences", output.ToArray());
        }));
        using var client = new HttpClient { BaseAddress = new Uri(BaseUrl()) };
        Content = await BrowserContent.LoadAsync(client, Option("art"));
        SeamApp.ConfigureGraphics(Content.Atlas, Content.Fonts, Content.Art);
        if (Option("showcase") is { } showcase)
        {
            SeamApp.ConfigureShowcase(showcase);
        }

        if (Option("observe") == "1")
        {
            SeamApp.Observe = value => observed = value;
        }

        await AppBuilder.Configure<SeamApp>().WithInterFont()
            .StartBrowserAppAsync("out", new BrowserPlatformOptions { RenderingMode = [backend] });
    }
}
