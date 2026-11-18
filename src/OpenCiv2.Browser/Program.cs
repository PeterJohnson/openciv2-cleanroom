using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Browser;
using OpenCiv2.App;

internal static partial class Program
{
    [JSExport]
    public static string Identity() => ApplicationIdentity.Read(typeof(Program).Assembly);
    [JSExport]
    public static string Snapshot() => SeamApp.CurrentView?.Snapshot() ?? "{}";
    public static async Task Main() => await AppBuilder.Configure<SeamApp>().WithInterFont()
        .StartBrowserAppAsync("out", new BrowserPlatformOptions { RenderingMode = [BrowserRenderingMode.WebGL2] });
}
