namespace OpenCiv2.IO.Native;

/// <summary>Private application store. Each value is staged, flushed and atomically replaced on one filesystem.
/// This does not replace ClassicStorage's required partial-write semantics.</summary>
public sealed class NativeByteStore(string directory) : IByteStore
{
    private readonly string root = Path.GetFullPath(directory);
    private readonly SemaphoreSlim gate = new(1, 1);
    private string PathFor(string key)
    {
        StorageKey.Validate(key);
        return Path.Combine(root, key + ".oc2data");
    }
    public async Task<byte[]?> ReadAsync(string key)
    {
        string path = PathFor(key);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            try
            {
                await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                    65536, FileOptions.Asynchronous);
                if (input.Length > StoredDocument.MaximumEnvelopeBytes)
                {
                    throw new IOException("Stored value exceeds 32 MiB");
                }

                byte[] bytes = new byte[checked((int)input.Length)];
                await input.ReadExactlyAsync(bytes).ConfigureAwait(false);
                return bytes;
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }
        finally { gate.Release(); }
    }
    public Task WriteAsync(string key, ReadOnlyMemory<byte> bytes)
    {
        string path = PathFor(key);
        if (bytes.Length > StoredDocument.MaximumEnvelopeBytes)
        {
            throw new IOException("Stored value exceeds 32 MiB");
        }

        byte[] owned = bytes.ToArray();
        // Flush(true) and rename are native blocking operations, kept off the application dispatcher.
        return Task.Run(async () =>
        {
            await gate.WaitAsync().ConfigureAwait(false);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(root);
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
                if (!OperatingSystem.IsWindows())
                {
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                }

                using (var output = new FileStream(temporary, options))
                {
                    output.Write(owned);
                    output.Flush(flushToDisk: true);
                }
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                try
                { File.Delete(temporary); }
                finally { gate.Release(); }
            }
        });
    }
    public async Task RemoveAsync(string key)
    {
        string path = PathFor(key);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        { File.Delete(path); }
        catch (DirectoryNotFoundException) { }
        finally { gate.Release(); }
    }
    public async Task<string[]> ListAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(root))
            {
                return [];
            }

            return Directory.EnumerateFiles(root, "*.oc2data").Select(Path.GetFileNameWithoutExtension)
                .Select(key => { StorageKey.Validate(key!); return key!; }).Order(StringComparer.Ordinal).ToArray();
        }
        finally { gate.Release(); }
    }
}
