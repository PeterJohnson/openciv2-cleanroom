using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Browser;
using OpenCiv2.App;

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
            .StartBrowserAppAsync("out", new BrowserPlatformOptions { RenderingMode = [BrowserRenderingMode.WebGL2] });
    }
}
