using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OpenCiv2.IO.Native;

/// <summary>Console's Unix stream suppresses broken-pipe errors; the CLI contract must report them.</summary>
public static partial class StandardOutput
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GetStdHandle(int handle);

    public static Stream Open()
    {
        nint raw = OperatingSystem.IsWindows() ? GetStdHandle(-11) : 1;
        var handle = new SafeFileHandle(raw, ownsHandle: false);
        try
        {
            return new FileStream(handle, System.IO.FileAccess.Write, bufferSize: 4096, isAsync: false);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }
}
