using System.Text;
using OpenCiv2.Automation;
using OpenCiv2.Base;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Content;
using OpenCiv2.Session;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args is ["--replay", var trace, "--output", var output])
            {
                byte[] recording = ReadBounded(trace, LocalReplayCodec.MaximumBytes);
                WriteNew(output, LocalReplay.Run(LocalReplayCodec.Decode(recording)));
                return 0;
            }
            if (args is not ["--script", var path, ..] || args.Skip(2).Contains("--game"))
            {
                System.Console.Error.WriteLine("Usage: OpenCiv2.Desktop.Automation --script PATH [--record TRACE] [desktop options]");
                System.Console.Error.WriteLine("       OpenCiv2.Desktop.Automation --replay TRACE --output SAVE");
                return 2;
            }
            return RunScript(path, args[2..]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or ContentException or InvalidDataException)
        {
            System.Console.Error.WriteLine(error.Message);
            return 2;
        }
    }

    private static int RunScript(string path, string[] options)
    {
        byte[] bytes = ReadBounded(path, 65536);
        int start = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? Encoding.UTF8.Preamble.Length : 0;
        string source = new UTF8Encoding(false, true).GetString(bytes.AsSpan(start));
        var recorder = options is ["--record", _, ..] ? new LocalReplayRecorder(ContentIdentity.Sha256(bytes)) : null;
        string? recordPath = recorder is not null ? options[1] : null;
        if (recordPath is not null && File.Exists(recordPath))
        {
            throw new IOException("The replay output already exists.");
        }
        int result = DesktopHost.Run(["--game", "alder-valley", .. (recorder is null ? options : options[2..])],
            (game, panel) => new DesktopScript(game, panel, source, Path.GetFileName(path)),
            recorder, typeof(Program).Assembly, typeof(ScriptTask).Assembly);
        if (result == 0 && recorder is not null && recordPath is not null)
        {
            WriteNew(recordPath, LocalReplayCodec.Encode(recorder.Capture()));
        }
        return result;
    }

    private static byte[] ReadBounded(string path, int limit)
    {
        using var file = File.OpenRead(path);
        long length = file.Length;
        if (length > limit)
        {
            throw new ArgumentException($"Input exceeds the {limit}-byte limit.");
        }
        var bytes = new byte[(int)length];
        file.ReadExactly(bytes);
        if (file.ReadByte() != -1)
        {
            throw new IOException("Input changed while reading.");
        }
        return bytes;
    }

    private static void WriteNew(string path, ReadOnlySpan<byte> bytes)
    {
        string destination = Path.GetFullPath(path);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".oc2-{Guid.NewGuid():N}.tmp");
        bool created = false;
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew))
            {
                created = true;
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: false);
            created = false;
        }
        finally
        {
            if (created)
            {
                File.Delete(temporary);
            }
        }
    }
}
