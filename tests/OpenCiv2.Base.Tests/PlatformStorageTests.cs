using OpenCiv2.IO;
using OpenCiv2.IO.Native;
using Xunit;

namespace OpenCiv2.Base.Tests;

public sealed class PlatformStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "oc2-storage-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    [Fact]
    public async Task Reopen_preserves_named_bytes_and_remove_completion()
    {
        var store = new NativeByteStore(root);
        Assert.Null(await store.ReadAsync("absent")); Assert.Empty(await store.ListAsync());
        var document = new StoredDocument("存档 É.SAV", [0, 255, 128, 0]);
        await store.WriteAsync("file-one", document.Encode());
        var reopened = new NativeByteStore(root);
        var decoded = StoredDocument.Decode((await reopened.ReadAsync("file-one"))!);
        Assert.Equal(document.Name, decoded.Name); Assert.Equal(document.Bytes, decoded.Bytes);
        Assert.Equal(new[] { "file-one" }, await reopened.ListAsync());
        await reopened.RemoveAsync("file-one"); await reopened.RemoveAsync("file-one");
        Assert.Empty(await store.ListAsync());
    }
    [Fact]
    public async Task Write_owns_input_and_replacement_readers_never_observe_partial_bytes()
    {
        var writer = new NativeByteStore(root); var reader = new NativeByteStore(root);
        byte[] first = Enumerable.Repeat((byte)1, 200000).ToArray(), second = Enumerable.Repeat((byte)2, 100000).ToArray();
        byte[] submitted = first.ToArray();
        var pending = writer.WriteAsync("value", submitted); Array.Fill(submitted, (byte)9); await pending;
        Assert.Equal(first, await reader.ReadAsync("value"));
        for (int i = 0; i < 10; i++)
        {
            pending = writer.WriteAsync("value", i % 2 == 0 ? second : first);
            byte[] bytes = (await reader.ReadAsync("value"))!;
            Assert.True(bytes.SequenceEqual(first) || bytes.SequenceEqual(second)); await pending;
        }
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Fact]
    public async Task Failed_publication_cleans_staging_and_path_keys_cannot_escape()
    {
        Directory.CreateDirectory(Path.Combine(root, "blocked.oc2data"));
        var store = new NativeByteStore(root);
        await Assert.ThrowsAsync<IOException>(() => store.WriteAsync("blocked", new byte[] { 7 }));
        Assert.True(Directory.Exists(Path.Combine(root, "blocked.oc2data")));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        foreach (string key in new[] { "../outside", "/absolute", "C:\\file", "UPPER" })
            await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync(key, new byte[] { 1 }));
    }
    [Fact]
    public async Task Selection_cancellation_rejects_late_reply_and_double_dispatch()
    {
        var transfer = new DeferredTransfer(); var store = new NativeByteStore(root);
        using var library = new DocumentLibrary(store, transfer);
        var first = library.ImportAsync();
        Assert.True(library.Busy); Assert.True(library.CanCancel);
        await library.ImportAsync(); Assert.Equal(1, transfer.Selections);
        library.CancelSelection();
        transfer.Selection.SetResult(new("late.sav", [1])); await first;
        Assert.Empty(library.Entries); Assert.Empty(await store.ListAsync());
        Assert.Equal("Import cancelled.", library.Message); Assert.False(library.Busy);
    }
    [Fact]
    public async Task Disposal_revokes_picker_but_commit_is_not_relabelled_cancelled()
    {
        var transfer = new DeferredTransfer(); var store = new DeferredStore();
        var library = new DocumentLibrary(store, transfer);
        var pending = library.ImportAsync(); library.Dispose();
        transfer.Selection.SetResult(new("late.sav", [1])); await pending;
        Assert.Equal(0, store.Writes);
        transfer = new DeferredTransfer(); library = new(store, transfer);
        pending = library.ImportAsync(); transfer.Selection.SetResult(new("accepted.sav", [2]));
        await store.Started.Task;
        Assert.False(library.CanCancel); Assert.Empty(library.Entries);
        library.CancelSelection(); store.Commit.SetResult(); await pending;
        Assert.Single(library.Entries); Assert.StartsWith("Stored", library.Message);
        library.Dispose();
    }
    [Fact]
    public async Task Commit_failure_never_publishes_success_or_an_entry()
    {
        var transfer = new DeferredTransfer(); var store = new DeferredStore();
        using var library = new DocumentLibrary(store, transfer);
        var pending = library.ImportAsync(); transfer.Selection.SetResult(new("failed.sav", [3]));
        await store.Started.Task; store.Commit.SetException(new IOException("authored quota error")); await pending;
        Assert.Empty(library.Entries); Assert.Contains("quota error", library.Message); Assert.False(library.Busy);
    }
    private sealed class DeferredTransfer : IFileTransfer
    {
        internal TaskCompletionSource<StoredDocument?> Selection { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Selections;
        public Task<StoredDocument?> ImportAsync(CancellationToken cancellation) { Selections++; return Selection.Task; }
        public Task<ExportResult> ExportAsync(StoredDocument document) => Task.FromResult(ExportResult.Cancelled);
    }
    private sealed class DeferredStore : IByteStore
    {
        internal TaskCompletionSource Commit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Writes;
        public Task<byte[]?> ReadAsync(string key) => Task.FromResult<byte[]?>(null);
        public Task WriteAsync(string key, ReadOnlyMemory<byte> bytes) { Writes++; Started.SetResult(); return Commit.Task; }
        public Task RemoveAsync(string key) => Task.CompletedTask;
        public Task<string[]> ListAsync() => Task.FromResult(Array.Empty<string>());
    }
}
