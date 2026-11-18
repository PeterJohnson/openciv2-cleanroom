using Avalonia;
using OpenCiv2.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Explicit read-only test observation; ordinary launches perform no report-file writes.
        if (args.Length == 2 && args[0] == "--observe")
        {
            string path = Path.GetFullPath(args[1]); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".identity.json", ApplicationIdentity.Read(typeof(Program).Assembly));
            string? previous = null;
            SeamApp.Observe = value =>
            {
                if (value == previous) return;
                File.WriteAllText(path + ".tmp", value); File.Move(path + ".tmp", path, true); previous = value;
            };
        }
        else if (args.Length != 0) throw new ArgumentException("Usage: OpenCiv2.Desktop [--observe PATH]");
        AppBuilder.Configure<SeamApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
    }
}
