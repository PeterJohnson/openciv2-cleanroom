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
    public async Task Replacement_keeps_an_open_reader_on_the_complete_previous_value()
    {
        // Architecture §§7–8 / IByteStore: publication changes the named value,
        // without mutating the bytes already opened by another reader.
        var store = new NativeByteStore(root);
        byte[] original = Enumerable.Repeat((byte)1, 200000).ToArray();
        byte[] replacement = Enumerable.Repeat((byte)2, 100000).ToArray();
        await store.WriteAsync("value", original);

        await using var reader = new FileStream(Path.Combine(root, "value.oc2data"),
            FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, FileOptions.Asynchronous);
        Assert.Equal(original[0], reader.ReadByte());

        await store.WriteAsync("value", replacement);

        byte[] remaining = new byte[original.Length - 1];
        await reader.ReadExactlyAsync(remaining, TestContext.Current.CancellationToken);
        Assert.Equal(original[1..], remaining);
        Assert.Equal(-1, reader.ReadByte());
        Assert.Equal(replacement, await new NativeByteStore(root).ReadAsync("value"));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Fact]
    public async Task Two_first_publishers_can_create_then_replace_a_complete_value()
    {
        // Architecture §§7–8 / IByteStore: cover a destination appearing between
        // initial publication attempts, not general multi-writer coordination.
        byte[][] values = Enumerable.Range(1, 2)
            .Select(value => Enumerable.Repeat((byte)value, value * 10000).ToArray()).ToArray();
        for (int round = 0; round < 10; round++)
        {
            string key = "new-" + round;
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task[] writes = values.Select(async bytes =>
            {
                var store = new NativeByteStore(root);
                await start.Task;
                await store.WriteAsync(key, bytes);
            }).ToArray();
            start.SetResult();
            await Task.WhenAll(writes);

            byte[]? published = await new NativeByteStore(root).ReadAsync(key);
            Assert.NotNull(published);
            Assert.Contains(values, bytes => bytes.SequenceEqual(published));
        }
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    public static bool WindowsFileSharing => OperatingSystem.IsWindows();
    [Fact(Skip = "Windows enforces handle delete-sharing during replacement.", SkipUnless = nameof(WindowsFileSharing))]
    public async Task Denied_replacement_preserves_the_previous_value_and_releases_staging()
    {
        // Architecture §§7–8 / IByteStore: an actual publication failure must
        // preserve committed data and leave the store usable for a later write.
        var store = new NativeByteStore(root);
        byte[] original = [1, 2, 3], replacement = [4, 5];
        await store.WriteAsync("value", original);
        using (var reader = new FileStream(Path.Combine(root, "value.oc2data"),
            FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Exception? failure = await Record.ExceptionAsync(() => store.WriteAsync("value", replacement));
            Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString() ?? "Write succeeded");
            Assert.Equal(original, await store.ReadAsync("value"));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        await store.WriteAsync("value", replacement);
        Assert.Equal(replacement, await store.ReadAsync("value"));
    }
    [Fact]
    public async Task Failed_publication_cleans_staging_and_path_keys_cannot_escape()
    {
        Directory.CreateDirectory(Path.Combine(root, "blocked.oc2data"));
        var store = new NativeByteStore(root);
        Exception? failure = await Record.ExceptionAsync(() => store.WriteAsync("blocked", new byte[] { 7 }));
        // Directory collisions can be access-denied on Windows or an I/O error
        // on Unix. The storage contract requires failure and cleanup, not one OS error type.
        Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString() ?? "Write succeeded");
        Assert.True(Directory.Exists(Path.Combine(root, "blocked.oc2data")));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        foreach (string key in new[] { "../outside", "/absolute", "C:\\file", "UPPER" })
            await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync(key, new byte[] { 1 }));
        await store.WriteAsync("after-failure", new byte[] { 9 });
        Assert.Equal(new byte[] { 9 }, await store.ReadAsync("after-failure"));
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
