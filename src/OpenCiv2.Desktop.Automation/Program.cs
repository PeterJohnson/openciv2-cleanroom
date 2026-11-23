using System.Text;
using OpenCiv2.Automation;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args is not ["--script", var path, ..] || args.Skip(2).Contains("--game"))
        {
            System.Console.Error.WriteLine("Usage: OpenCiv2.Desktop.Automation --script PATH [desktop options]");
            return 2;
        }
        try
        {
            string source = ReadScript(path);
            return DesktopHost.Run(["--game", "alder-valley", .. args.Skip(2)],
                (game, panel) => new DesktopScript(game, panel, source, Path.GetFileName(path)),
                typeof(Program).Assembly, typeof(ScriptTask).Assembly);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            System.Console.Error.WriteLine(error.Message);
            return 2;
        }
    }

    private static string ReadScript(string path)
    {
        using var file = File.OpenRead(path);
        byte[] bytes = new byte[65537];
        int count = file.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        if (count == bytes.Length)
        {
            throw new ArgumentException("Script exceeds 64 KiB.");
        }
        int start = bytes.AsSpan(0, count).StartsWith(Encoding.UTF8.Preamble) ? Encoding.UTF8.Preamble.Length : 0;
        return new UTF8Encoding(false, true).GetString(bytes, start, count - start);
    }
}
