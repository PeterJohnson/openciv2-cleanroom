using OpenCiv2.Base;
using OpenCiv2.IO;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // Architecture §§7–8, F06 production file-choice boundary, P10 media obligations.
    private static void DocumentEnvelope()
    {
        byte[] bytes = [0, 255, 128, 13, 10, 0, 42];
        var file = new StoredDocument("存档 É.SAV", bytes);
        bytes[0] = 9;
        var encoded = file.Encode();
        var decoded = StoredDocument.Decode(encoded);
        Check(decoded.Name == "存档 É.SAV" && decoded.Bytes.SequenceEqual(new byte[] { 0, 255, 128, 13, 10, 0, 42 }), "owned unchanged payload");
        encoded[^1] = 0;
        Check(decoded.Bytes[^1] == 42, "decoded bytes owned");
        foreach (string name in new[] { "../bad.sav", "C:\\bad.sav", "x/y", "", "..", "a\0b" })
            Throws<ArgumentException>(() => new StoredDocument(name, []));
        foreach (string key in new[] { "../x", "A", "a.b", "", "c:x", "x/y", "x\\y" })
            Throws<ArgumentException>(() => StorageKey.Validate(key));
        StorageKey.Validate("file-abc123");
        for (int i = 0; i < 12; i++) Throws<InvalidDataException>(() => StoredDocument.Decode(encoded.AsSpan(0, i)));
        encoded[8] = 255; encoded[9] = 255;
        Throws<InvalidDataException>(() => StoredDocument.Decode(encoded));
    }
    private static void DocumentWorkflow()
    {
        var store = new MemoryByteStore();
        var transfer = new ImmediateTransfer { Document = new("TERRAIN1.GIF", [1, 2, 3, 0]) };
        using var library = new DocumentLibrary(store, transfer);
        Complete(library.ImportAsync());
        Check(library.Entries.Count == 1 && !library.Busy && library.Message.StartsWith("Stored", StringComparison.Ordinal), "commit before saved message");
        string key = library.Entries[0].Key;
        transfer.Document = null;
        Complete(library.ImportAsync());
        Check(library.Entries.Count == 1 && library.Message == "Import cancelled.", "cancel changes no stored files");
        using var reopened = new DocumentLibrary(store, transfer);
        Complete(reopened.RefreshAsync());
        Check(reopened.Entries.Equals(library.Entries), "reopen stored identity");
        Complete(reopened.ExportAsync(key));
        Check(transfer.Exported!.Bytes.SequenceEqual(new byte[] { 1, 2, 3, 0 }) && reopened.Message.StartsWith("Download requested", StringComparison.Ordinal), "download not durable save");
        Complete(reopened.RemoveAsync(key));
        Check(reopened.Entries.Count == 0 && store.Values.Count == 0, "remove committed copy");
        transfer.Document = new("failed.sav", [7]); store.FailWrites = true;
        Complete(reopened.ImportAsync());
        Check(reopened.Entries.Count == 0 && reopened.Message.StartsWith("File operation failed", StringComparison.Ordinal), "failure never publishes saved");
        store.Values["file-corrupt"] = [0, 1, 2];
        Complete(reopened.RefreshAsync());
        Check(!reopened.Busy && reopened.Entries.Count == 0 && reopened.Message.StartsWith("File operation failed", StringComparison.Ordinal), "corrupt stored envelope is a visible recoverable failure");
    }
    private static void ClockAndSilentMedia()
    {
        var clock = new ManualPlatformClock(new(2024, 1, 1, 23, 59, 59, TimeSpan.FromHours(2)));
        Check(clock.Milliseconds == 0 && clock.Milliseconds == 0, "reads never advance time");
        clock.Advance(2000);
        Check(clock.Milliseconds == 2000 && clock.LocalNow.Day == 2 && clock.LocalNow.Offset == TimeSpan.FromHours(2), "explicit monotonic/calendar progression");
        var silent = new SilentMedia();
        var request = new MediaRequest("authored-wonder", "Meaningful caption remains with caller");
        var result = silent.PlayAsync(request);
        Complete(result);
        Check(result.Result == MediaResult.Skipped && request.Caption.Contains("Meaningful", StringComparison.Ordinal), "silent completion without changing notice");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Check(silent.PlayAsync(request, cancellation.Token).Result == MediaResult.Cancelled, "cancel completes explicitly");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Complete(Task task)
    {
        Check(task.IsCompleted, "authored immediate provider must complete synchronously in native/WASM");
        task.GetAwaiter().GetResult();
    }
    private sealed class MemoryByteStore : IByteStore
    {
        internal Dictionary<string, byte[]> Values { get; } = [];
        internal bool FailWrites;
        public Task<byte[]?> ReadAsync(string key) => Task.FromResult(Values.TryGetValue(key, out var value) ? value.ToArray() : null);
        public Task WriteAsync(string key, ReadOnlyMemory<byte> bytes)
        {
            if (FailWrites) throw new IOException("authored quota failure");
            Values[key] = bytes.ToArray(); return Task.CompletedTask;
        }
        public Task RemoveAsync(string key) { Values.Remove(key); return Task.CompletedTask; }
        public Task<string[]> ListAsync() => Task.FromResult(Values.Keys.ToArray());
    }
    private sealed class ImmediateTransfer : IFileTransfer
    {
        internal StoredDocument? Document, Exported;
        public Task<StoredDocument?> ImportAsync(CancellationToken cancellation) => Task.FromResult(Document);
        public Task<ExportResult> ExportAsync(StoredDocument document)
        { Exported = document; return Task.FromResult(ExportResult.DownloadStarted); }
    }
}
