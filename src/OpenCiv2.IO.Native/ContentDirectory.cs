using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using OpenCiv2.Base;

namespace OpenCiv2.IO.Native;

public sealed record NativeContentSource(string Identity, OwnedList<byte> Bytes);

/// <summary>Explicit native mount, contained regular files, bounded reads and backing-file identity.</summary>
public sealed class ContentDirectory
{
    private readonly string root;
    private readonly Dictionary<string, string> identities = new(StringComparer.Ordinal);
    public ContentDirectory(string mountedRoot)
    {
        root = FileMetadata.Canonical(mountedRoot);
        if (!Directory.Exists(root)) throw new ArgumentException("Content mount must be a directory");
    }
    private static void Relative(string path)
    {
        if (path.Length == 0 || path[0] == '/' || path.Contains('\\') || path.Any(c => c < 32 || c == 127) ||
            path.Split('/').Any(part => part is "" or "." or "..") || (OperatingSystem.IsWindows() && path.Contains(':')))
            throw new ArgumentException("Expected a relative slash-separated mounted path");
        int colon = path.IndexOf(':');
        if (colon > 0 && char.IsAsciiLetter(path[0]) && path[..colon].All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.'))
            throw new ArgumentException("Content paths cannot use URL schemes");
    }
    private static void Contained(string path, string directory)
    {
        // Canonical paths resolve every intermediate symlink. Match ancestors by
        // backing identity, retaining Windows case/volume semantics.
        string? parent = Path.GetDirectoryName(path);
        string rootIdentity = FileMetadata.PathIdentity(directory);
        while (parent is not null)
        {
            if (FileMetadata.PathIdentity(parent) == rootIdentity) return;
            parent = Path.GetDirectoryName(parent);
        }
        throw new ArgumentException("Content path leaves its mounted directory");
    }
    public NativeContentSource Read(string relative, int maximumBytes = 16 * 1024 * 1024, string requiredDirectory = "")
    {
        Relative(relative);
        if (maximumBytes < 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        string path = FileMetadata.Canonical(Path.Combine(root, relative));
        Contained(path, root);
        if (requiredDirectory.Length != 0)
        {
            Relative(requiredDirectory);
            Contained(path, FileMetadata.Canonical(Path.Combine(root, requiredDirectory)));
        }
        FileMetadata.RequireRegular(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        string identity = FileMetadata.HandleIdentity(input.SafeFileHandle);
        using var output = new MemoryStream(); var buffer = new byte[8192];
        for (int count; (count = input.Read(buffer)) != 0;)
        {
            if (count > maximumBytes - output.Length) throw new IOException("Content source exceeds configured byte limit");
            output.Write(buffer, 0, count);
        }
        // Receipts use the first canonical path for this backing file within the
        // mount. Native IDs stay internal, preserving hardlink alias detection.
        if (!identities.TryGetValue(identity, out string? sourcePath))
        {
            sourcePath = path;
            if (OperatingSystem.IsWindows())
            {
                if (sourcePath.StartsWith(@"\\?\UNC\", StringComparison.Ordinal)) sourcePath = @"\\" + sourcePath[8..];
                else if (sourcePath.StartsWith(@"\\?\", StringComparison.Ordinal)) sourcePath = sourcePath[4..];
            }
            sourcePath = sourcePath.Replace(Path.DirectorySeparatorChar, '/');
            identities.Add(identity, sourcePath);
        }
        return new(sourcePath, new(output.ToArray()));
    }
}

// Narrow OS ABI boundary: Linux statx, Darwin stat64, Windows file ID information.
// Layout/source references are recorded in docs/native-file-identity.md.
internal static unsafe partial class FileMetadata
{
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStat
    {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(28)] public ushort Mode;
        [FieldOffset(32)] public ulong Inode;
        [FieldOffset(136)] public uint DeviceMajor;
        [FieldOffset(140)] public uint DeviceMinor;
    }
    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct DarwinStat
    {
        [FieldOffset(0)] public uint Device;
        [FieldOffset(4)] public ushort Mode;
        [FieldOffset(8)] public ulong Inode;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsId { public ulong Volume, Low, High; }
    [LibraryImport("libc", EntryPoint = "statx", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Statx(int fd, string path, int flags, uint mask, out LinuxStat value);
    [LibraryImport("libc", EntryPoint = "stat64", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Stat64(string path, out DarwinStat value);
    [LibraryImport("libc", EntryPoint = "fstat64", SetLastError = true)]
    private static partial int Fstat64(int fd, out DarwinStat value);
    [LibraryImport("libc", EntryPoint = "stat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int StatArm64(string path, out DarwinStat value);
    [LibraryImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static partial int FstatArm64(int fd, out DarwinStat value);
    private static int DarwinPath(string path, out DarwinStat value) => RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        ? StatArm64(path, out value) : Stat64(path, out value);
    private static int DarwinHandle(int fd, out DarwinStat value) => RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        ? FstatArm64(fd, out value) : Fstat64(fd, out value);
    [LibraryImport("libc", EntryPoint = "realpath", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial nint Realpath(string path, nint buffer);
    [LibraryImport("libc", EntryPoint = "free")]
    private static partial void Free(nint memory);
    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFile(string path, uint access, uint sharing, nint security, uint creation, uint flags, nint template);
    [LibraryImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true)]
    private static partial uint FinalPath(SafeFileHandle handle, char* buffer, uint size, uint flags);
    [LibraryImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FileId(SafeFileHandle handle, int informationClass, out WindowsId identity, uint size);
    private static IOException Failure() => new(new Win32Exception(Marshal.GetLastPInvokeError()).Message);
    private static SafeFileHandle OpenMetadata(string path)
    {
        var handle = CreateFile(path, 0, 7, 0, 3, 0x02000000, 0);
        if (handle.IsInvalid) { var failure = Failure(); handle.Dispose(); throw failure; }
        return handle;
    }
    public static string Canonical(string path)
    {
        // Canonical Windows mounts use the extended path prefix, which skips
        // separator normalization. Convert admitted portable separators before
        // passing a nested mounted path to the native API.
        path = Path.GetFullPath(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        if (OperatingSystem.IsWindows())
        {
            using var handle = OpenMetadata(path);
            char[] buffer = new char[32768];
            fixed (char* data = buffer)
            {
                uint count = FinalPath(handle, data, (uint)buffer.Length, 0);
                if (count == 0) throw Failure();
                if (count >= buffer.Length) throw new IOException("Content path exceeds native path limit");
                return new string(data, 0, (int)count);
            }
        }
        nint resolved = Realpath(path, 0);
        if (resolved == 0) throw Failure();
        try { return Marshal.PtrToStringUTF8(resolved)!; } finally { Free(resolved); }
    }
    private static string LinuxIdentity(LinuxStat value)
    {
        if ((value.Mask & 0x101) != 0x101) throw new IOException("Filesystem did not return file identity/type");
        return $"unix:{value.DeviceMajor:x}:{value.DeviceMinor:x}:{value.Inode:x}";
    }
    public static string PathIdentity(string path)
    {
        if (OperatingSystem.IsWindows()) { using var handle = OpenMetadata(path); return HandleIdentity(handle); }
        if (OperatingSystem.IsLinux())
        {
            if (Statx(-100, path, 0, 0x101, out var value) != 0) throw Failure();
            return LinuxIdentity(value);
        }
        if (OperatingSystem.IsMacOS())
        {
            if (DarwinPath(path, out var value) != 0) throw Failure();
            return $"unix:{value.Device:x}:{value.Inode:x}";
        }
        throw new PlatformNotSupportedException("Native content supports Linux, macOS and Windows");
    }
    public static string HandleIdentity(SafeFileHandle handle)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!FileId(handle, 18, out var value, 24)) throw Failure();
            return $"windows:{value.Volume:x}:{value.High:x16}{value.Low:x16}";
        }
        bool held = false;
        try
        {
            handle.DangerousAddRef(ref held); int fd = checked((int)handle.DangerousGetHandle());
            if (OperatingSystem.IsLinux())
            {
                if (Statx(fd, "", 0x1000, 0x101, out var value) != 0) throw Failure();
                return LinuxIdentity(value);
            }
            if (OperatingSystem.IsMacOS())
            {
                if (DarwinHandle(fd, out var value) != 0) throw Failure();
                return $"unix:{value.Device:x}:{value.Inode:x}";
            }
            throw new PlatformNotSupportedException("Native content supports Linux, macOS and Windows");
        }
        finally { if (held) handle.DangerousRelease(); }
    }
    public static void RequireRegular(string path)
    {
        bool regular;
        if (OperatingSystem.IsWindows()) regular = (File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.Device)) == 0;
        else if (OperatingSystem.IsLinux())
        {
            if (Statx(-100, path, 0, 1, out var value) != 0) throw Failure();
            regular = (value.Mask & 1) != 0 && (value.Mode & 0xf000) == 0x8000;
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (DarwinPath(path, out var value) != 0) throw Failure();
            regular = (value.Mode & 0xf000) == 0x8000;
        }
        else throw new PlatformNotSupportedException("Native content supports Linux, macOS and Windows");
        if (!regular) throw new ArgumentException("Content source must be a regular file");
    }
}
