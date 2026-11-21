using System.Buffers.Binary;
using System.Text;
using OpenCiv2.Base;

namespace OpenCiv2.IO;

/// <summary>Application-owned bytes, separate from the synchronous F03/F06 partial-effect streams.
/// A write completes only after publication/persistence. Failed replacement preserves the prior value.
/// Keys are opaque application identities, never platform paths. Results own their bytes.</summary>
public interface IByteStore
{
    Task<byte[]?> ReadAsync(string key);
    Task WriteAsync(string key, ReadOnlyMemory<byte> bytes);
    Task RemoveAsync(string key);
    Task<string[]> ListAsync();
}

public static class StorageKey
{
    public static void Validate(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 100 ||
            key.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
        {
            throw new ArgumentException("Invalid application storage key", nameof(key));
        }
    }
}

/// <summary>Application envelope; the enclosed classic/native file is never rewritten.</summary>
public sealed class StoredDocument
{
    public const int MaximumBytes = 32 * 1024 * 1024;
    public const int MaximumEnvelopeBytes = MaximumBytes + 1036;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public string Name { get; }
    public OwnedList<byte> Bytes { get; }
    public StoredDocument(string name, IEnumerable<byte> bytes)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Length > 255 ||
            name.Any(c => char.IsControl(c) || c is '/' or '\\' or ':'))
        {
            throw new ArgumentException("Choose a file with a plain filename", nameof(name));
        }

        Name = name;
        Bytes = new(bytes);
        if (Bytes.Count > MaximumBytes)
        {
            throw new IOException("Files must be at most 32 MiB");
        }
    }
    public byte[] Encode()
    {
        byte[] name = Utf8.GetBytes(Name);
        byte[] result = new byte[12 + name.Length + Bytes.Count];
        "OC2DOC01"u8.CopyTo(result);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(8), name.Length);
        name.CopyTo(result, 12);
        for (int i = 0; i < Bytes.Count; i++)
        {
            result[12 + name.Length + i] = Bytes[i];
        }

        return result;
    }
    public static StoredDocument Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || bytes.Length > MaximumEnvelopeBytes || !bytes[..8].SequenceEqual("OC2DOC01"u8))
        {
            throw new InvalidDataException("Invalid stored document");
        }

        int size = BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]);
        if (size < 1 || size > 1024 || size > bytes.Length - 12)
        {
            throw new InvalidDataException("Invalid document name");
        }

        return new(Utf8.GetString(bytes.Slice(12, size)), bytes[(12 + size)..].ToArray());
    }
}

public enum ExportResult { Cancelled, Written, DownloadStarted }

/// <summary>Selection/read and export use host capabilities, not invented browser paths.
/// Cancellation of selection must discard a late result and release selected resources.</summary>
public interface IFileTransfer
{
    Task<StoredDocument?> ImportAsync(CancellationToken cancellation);
    Task<ExportResult> ExportAsync(StoredDocument document);
}

public sealed record DocumentEntry(string Key, string Name, int Size)
{
    public override string ToString() => $"{Name}  ·  {Size:N0} bytes";
}

/// <summary>One asynchronous file operation at a time. Cancellation ends selection only;
/// persistence and exports report their actual completion, including late errors.</summary>
public sealed class DocumentLibrary(IByteStore store, IFileTransfer transfer) : IDisposable
{
    private CancellationTokenSource? selection;
    private bool disposed;
    public void Dispose() { disposed = true; CancelSelection(); Changed = null; }
    public bool Busy { get; private set; }
    public bool CanCancel => selection is not null;
    public string Message { get; private set; } = "Imported files are stored here; opening a playable game is not available yet.";
    public OwnedList<DocumentEntry> Entries { get; private set; } = OwnedList<DocumentEntry>.Empty;
    public event Action? Changed;

    public void CancelSelection() => selection?.Cancel();

    public Task RefreshAsync() => Run(async () =>
    {
        await ReadEntries();
    });

    public Task ImportAsync() => Run(async () =>
    {
        using var cancellation = new CancellationTokenSource();
        selection = cancellation;
        Message = "Choose a file to import…";
        Changed?.Invoke();
        StoredDocument? document;
        try
        {
            document = await transfer.ImportAsync(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
        }
        finally { selection = null; }
        if (document is null)
        { Message = "Import cancelled."; return; }
        Message = "Saving imported file…";
        Changed?.Invoke();
        string key = "file-" + Guid.NewGuid().ToString("N");
        await store.WriteAsync(key, document.Encode());
        // Publish the committed entry before any fallible refresh. Never report a committed write as cancelled.
        Entries = new(Entries.Append(new(key, document.Name, document.Bytes.Count)));
        Message = "Stored " + document.Name + ".";
    });

    public Task ExportAsync(string key) => Run(async () =>
    {
        RequireEntry(key);
        byte[] bytes = await store.ReadAsync(key) ?? throw new IOException("The stored file no longer exists");
        var result = await transfer.ExportAsync(StoredDocument.Decode(bytes));
        Message = result switch
        {
            ExportResult.Written => "Export completed.",
            ExportResult.DownloadStarted => "Download requested. Check your browser downloads for completion.",
            _ => "Export cancelled."
        };
    });

    public Task RemoveAsync(string key) => Run(async () =>
    {
        RequireEntry(key);
        await store.RemoveAsync(key);
        Entries = new(Entries.Where(entry => entry.Key != key));
        Message = "Removed the stored copy.";
    });

    private void RequireEntry(string key)
    {
        if (!Entries.Any(entry => entry.Key == key))
        {
            throw new ArgumentException("Select a stored file");
        }
    }
    private async Task ReadEntries()
    {
        var next = new List<DocumentEntry>();
        foreach (string key in (await store.ListAsync()).Where(key => key.StartsWith("file-", StringComparison.Ordinal)))
        {
            byte[]? bytes = await store.ReadAsync(key);
            if (bytes is null)
            {
                continue;
            }

            var document = StoredDocument.Decode(bytes);
            next.Add(new(key, document.Name, document.Bytes.Count));
        }
        Entries = new(next.OrderBy(entry => entry.Name, StringComparer.Ordinal).ThenBy(entry => entry.Key, StringComparer.Ordinal));
    }
    private async Task Run(Func<Task> operation)
    {
        if (Busy || disposed)
        {
            return;
        }

        Busy = true;
        Changed?.Invoke();
        try
        { await operation(); }
        catch (OperationCanceledException) { Message = "Import cancelled."; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { Message = "File operation failed: " + error.Message; }
        finally { Busy = false; selection = null; Changed?.Invoke(); }
    }
}
