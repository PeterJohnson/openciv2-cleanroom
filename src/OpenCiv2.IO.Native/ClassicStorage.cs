using OpenCiv2.Base;

namespace OpenCiv2.IO.Native;

/// <summary>Native F03/F06 byte storage with an explicit working directory and ASCII case aliases.
/// Replacement truncates an existing file; it neither publishes atomically nor changes process cwd.</summary>
public sealed class ClassicStorage : IFileStorage
{
    public string Working { get; private set; }
    public ClassicStorage(string working)
    {
        if (!Directory.Exists(working)) throw new ArgumentException("Classic working path must be a directory", nameof(working));
        Working = FileMetadata.Canonical(working);
    }
    public static string ClassicPath(string native) => Path.GetFullPath(native).Replace('/', '\\');
    private static string NativePath(string path)
    {
        if (path.Contains('\0')) throw new ArgumentException("Classic path contains NUL", nameof(path));
        return path.Replace('\\', Path.DirectorySeparatorChar);
    }
    public static bool PathEqual(string a, string b)
    {
        if (a.Length != b.Length) return false;
        static char Upper(char c) => c is >= 'a' and <= 'z' ? (char)(c - 'a' + 'A') : c;
        for (int i = 0; i < a.Length; ++i) if (Upper(a[i]) != Upper(b[i])) return false;
        return true;
    }
    private string? Resolve(string directory, string name, bool missingLeaf = false)
    {
        string path = NativePath(name);
        if (!Path.IsPathFullyQualified(path)) path = Path.Combine(NativePath(directory), path);
        if (!Path.IsPathFullyQualified(path)) path = Path.Combine(Working, path);
        string selected = Path.GetPathRoot(path)!;
        string[] parts = path[selected.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; ++i)
        {
            string part = parts[i]; if (part == ".") continue;
            // Each traversed directory was resolved before interpreting '..', including symlinks.
            if (part == "..")
            {
                string root = Path.GetPathRoot(selected)!; string trimmed = selected.TrimEnd(Path.DirectorySeparatorChar);
                selected = trimmed == root.TrimEnd(Path.DirectorySeparatorChar) ? root : Path.GetDirectoryName(trimmed) ?? root; continue;
            }
            string? match = null;
            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(selected))
                    if (PathEqual(Path.GetFileName(entry), part))
                    { if (match is not null) throw new ArgumentException("Ambiguous classic filename case aliases"); match = entry; }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
            if (match is null && OperatingSystem.IsWindows())
            {
                // Native DOS short names need not appear in directory enumeration.
                string alias = Path.Combine(selected, part); if (File.Exists(alias) || Directory.Exists(alias)) match = alias;
            }
            if (match is null) return missingLeaf && i == parts.Length - 1 ? Path.Combine(selected, part) : null;
            selected = match;
            if (i != parts.Length - 1)
            {
                if (!Directory.Exists(selected)) return null;
                try { selected = FileMetadata.Canonical(selected); } catch (IOException) { return null; }
            }
        }
        return selected;
    }
    private static bool Regular(string path)
    {
        try { FileMetadata.RequireRegular(path); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }
    public void ChangeDirectory(string directory)
    {
        string? selected = Resolve("", directory);
        if (selected is null || !Directory.Exists(selected)) throw new IOException("Cannot select classic working directory");
        Working = FileMetadata.Canonical(selected);
    }
    public bool Exists(string directory, string name) => Resolve(directory, name) is { } path && Regular(path);
    /// <summary>Open an owned native stream for a format adapter. Caller disposes the returned stream.</summary>
    public FileStream? OpenReadStream(string directory, string name, out string resolvedPath)
    {
        resolvedPath = ""; string? path = Resolve(directory, name); if (path is null || !Regular(path)) return null;
        try { var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); resolvedPath = path; return stream; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
    public CoreInput? Read(string path) => OpenReadStream("", path, out _) is { } stream ? new Input(stream) : null;
    public CoreOutput? Replace(string path)
    {
        string? selected = Resolve("", path, true); if (selected is null) return null;
        if ((File.Exists(selected) || Directory.Exists(selected)) && !Regular(selected)) return null;
        try { return new Output(new FileStream(selected, FileMode.Create, FileAccess.Write, FileShare.Read)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
    public bool Remove(string path)
    {
        string? selected = Resolve("", path); if (selected is null || !Regular(selected)) return false;
        try { File.Delete(selected); return true; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
    private sealed class Input(FileStream stream) : CoreInput
    {
        private bool failed;
        protected override ReadResult ReadItem(int size)
        {
            var bytes = new byte[size]; int count = 0;
            try { while (count < size) { int n = stream.Read(bytes.AsSpan(count)); if (n == 0) break; count += n; } }
            catch (IOException) { failed = true; }
            return new(new(bytes.AsSpan(0, count).ToArray()), count == size);
        }
        protected override byte? ReadByte()
        {
            try { int value = stream.ReadByte(); return value < 0 ? null : (byte)value; } catch (IOException) { failed = true; return null; }
        }
        protected override bool Finish() { try { stream.Dispose(); } catch (IOException) { failed = true; } return !failed; }
    }
    private sealed class Output(FileStream stream) : CoreOutput
    {
        protected override bool WriteItem(ReadOnlySpan<byte> bytes) { try { stream.Write(bytes); return true; } catch (IOException) { return false; } }
        protected override bool Finish()
        {
            bool ok = true; try { stream.Flush(); } catch (IOException) { ok = false; }
            try { stream.Dispose(); } catch (IOException) { ok = false; }
            return ok;
        }
    }
}
