using System.ComponentModel;
using System.Runtime.InteropServices;
using OpenCiv2.Base;

namespace OpenCiv2.IO.Native;

public static partial class NativeFiles
{
    [LibraryImport("libc", EntryPoint = "link", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Link(string source, string destination);

    public static byte[] ReadFile(string path) => File.ReadAllBytes(path);

    /// <summary>Resolve the existing prefix of a new destination, including parent symlinks.</summary>
    public static string CanonicalNewPath(string path)
    {
        path = Path.GetFullPath(path);
        var suffix = new Stack<string>();
        for (; ; )
        {
            try
            {
                File.GetAttributes(path);
                break;
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                suffix.Push(Path.GetFileName(path));
                path = Path.GetDirectoryName(path) ?? throw new IOException("No existing output path prefix");
            }
        }
        path = FileMetadata.Canonical(path);
        while (suffix.Count != 0)
        {
            path = Path.Combine(path, suffix.Pop());
        }

        return path;
    }

    /// <summary>Stage, flush, close, then publish exclusively. Earlier outputs/effects are never rolled back.</summary>
    public static void WriteNewFile(
        string path,
        ReadOnlySpan<byte> bytes,
        StopToken stop = default,
        Action<int>? progress = null)
    {
        void Checkpoint(int written)
        {
            stop.CheckStop();
            progress?.Invoke(written);
            stop.CheckStop();
        }
        Checkpoint(0);
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new IOException("Export destination already exists");
        }

        string target = Path.GetFullPath(path);
        string? staging = null;
        FileStream? stream = null;
        try
        {
            for (int attempt = 0; attempt < 100; ++attempt)
            {
                string candidate = target + ".oc2-tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    var options = new FileStreamOptions
                    {
                        Mode = FileMode.CreateNew,
                        Access = System.IO.FileAccess.Write,
                        Share = FileShare.None
                    };
                    if (!OperatingSystem.IsWindows())
                    {
                        options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                    }

                    stream = new(candidate, options);
                    staging = candidate;
                    break;
                }
                catch (IOException) when (File.Exists(candidate) || Directory.Exists(candidate)) { }
            }
            if (stream is null)
            {
                throw new IOException("Cannot reserve an export staging file");
            }

            int written = 0;
            while (written < bytes.Length)
            {
                int size = Math.Min(bytes.Length - written, 64 * 1024);
                stream.Write(bytes.Slice(written, size));
                written += size;
                Checkpoint(written);
            }
            stream.Flush(flushToDisk: true);
            var closing = stream;
            stream = null;
            closing.Dispose();
            Checkpoint(written);
            if (OperatingSystem.IsWindows())
            {
                File.Move(staging!, target, overwrite: false);
                staging = null;
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // File.Move's Unix implementation may stat then rename, allowing a
                // competing publisher to be overwritten. link atomically reserves the
                // final name without exposing partial bytes. Finally removes our staging
                // name even after success; cleanup cannot undo the published link.
                if (Link(staging!, target) != 0)
                {
                    throw new IOException(
                        "Cannot publish output without overwrite",
                        new Win32Exception(Marshal.GetLastPInvokeError()));
                }
            }
            else
            {
                throw new PlatformNotSupportedException("Atomic publication requires Windows, Linux or macOS");
            }
        }
        finally
        {
            try
            {
                stream?.Dispose();
            }
            catch { }
            if (staging is not null)
            {
                try
                {
                    File.Delete(staging);
                }
                catch { }
            }
        }
    }
}
