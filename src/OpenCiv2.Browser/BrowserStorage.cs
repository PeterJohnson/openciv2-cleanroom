using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using OpenCiv2.IO;

internal sealed partial class BrowserStorage : IByteStore, IFileTransfer
{
    [JSImport("read", "storage")] private static partial Task<string?> Read(string key);
    [JSImport("write", "storage")] private static partial Task Write(string key, string bytes);
    [JSImport("remove", "storage")] private static partial Task Remove(string key);
    [JSImport("list", "storage")] private static partial Task<string> List();
    [JSImport("importFile", "storage")] private static partial Task<string?> ImportFile();
    [JSImport("cancelImport", "storage")] private static partial void CancelImport();
    [JSImport("exportFile", "storage")] private static partial void ExportFile(string name, string bytes);

    private static async Task<T> Io<T>(Func<Task<T>> operation)
    {
        try
        { return await operation(); }
        catch (JSException error) { throw new IOException(error.Message, error); }
    }
    private static async Task Io(Func<Task> operation)
    {
        try
        { await operation(); }
        catch (JSException error) { throw new IOException(error.Message, error); }
    }
    public async Task<byte[]?> ReadAsync(string key)
    {
        StorageKey.Validate(key);
        string? bytes = await Io(() => Read(key));
        if (bytes is null)
        {
            return null;
        }

        if (bytes.Length > (StoredDocument.MaximumEnvelopeBytes + 2L) / 3 * 4)
        {
            throw new IOException("Stored value exceeds 32 MiB");
        }

        try
        { return Convert.FromBase64String(bytes); }
        catch (FormatException error) { throw new IOException("Invalid stored bytes", error); }
    }
    public Task WriteAsync(string key, ReadOnlyMemory<byte> bytes)
    {
        StorageKey.Validate(key);
        if (bytes.Length > StoredDocument.MaximumEnvelopeBytes)
        {
            throw new IOException("Stored value exceeds 32 MiB");
        }

        string encoded = Convert.ToBase64String(bytes.Span);
        return Io(() => Write(key, encoded));
    }
    public Task RemoveAsync(string key) { StorageKey.Validate(key); return Io(() => Remove(key)); }
    public async Task<string[]> ListAsync()
    {
        using var json = JsonDocument.Parse(await Io(List));
        string[] keys = json.RootElement.EnumerateArray().Select(value => value.GetString()!).ToArray();
        foreach (string key in keys)
        {
            StorageKey.Validate(key);
        }

        return keys;
    }
    public async Task<StoredDocument?> ImportAsync(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var registration = cancellation.Register(CancelImport);
        string? result = await Io(ImportFile);
        cancellation.ThrowIfCancellationRequested();
        if (result is null)
        {
            return null;
        }

        using var json = JsonDocument.Parse(result);
        var root = json.RootElement;
        return new(root.GetProperty("name").GetString()!, Convert.FromBase64String(root.GetProperty("bytes").GetString()!));
    }
    public Task<ExportResult> ExportAsync(StoredDocument document)
    {
        try
        { ExportFile(document.Name, Convert.ToBase64String(document.Bytes.ToArray())); }
        catch (JSException error) { throw new IOException(error.Message, error); }
        return Task.FromResult(ExportResult.DownloadStarted);
    }
}
