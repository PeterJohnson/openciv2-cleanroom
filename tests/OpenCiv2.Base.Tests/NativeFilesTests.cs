using OpenCiv2.IO.Native;
using Xunit;

namespace OpenCiv2.Base.Tests;

public sealed class NativeFilesTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "oc2-native-" + Guid.NewGuid().ToString("N"));
    public NativeFilesTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, recursive: true);
    private string Destination => Path.Combine(directory, "存档 é.sav");
    private void NoStaging() => Assert.Empty(Directory.EnumerateFileSystemEntries(directory, "*.oc2-tmp-*"));

    [Fact]
    public void Publishes_exact_bytes_and_preserves_existing_destination()
    {
        byte[] bytes = Enumerable.Range(0, 140000).Select(i => unchecked((byte)i)).ToArray();
        var progress = new List<int>();
        NativeFiles.WriteNewFile(Destination, bytes, progress: progress.Add);
        Assert.Equal(bytes, NativeFiles.ReadFile(Destination));
        Assert.Equal(new[] { 0, 65536, 131072, 140000, 140000 }, progress);
        Assert.Throws<IOException>(() => NativeFiles.WriteNewFile(Destination, [1]));
        Assert.Equal(bytes, NativeFiles.ReadFile(Destination)); NoStaging();
    }
    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(70000)]
    public void Cancellation_removes_unpublished_work(int stopAt)
    {
        var source = new CancellationSource();
        Assert.Throws<CancelledException>(() => NativeFiles.WriteNewFile(Destination, new byte[70000], source.Token,
            count => { if (count >= stopAt) source.RequestStop(); }));
        Assert.False(File.Exists(Destination)); NoStaging();
    }
    [Fact]
    public void Cancellation_after_close_prevents_publication()
    {
        var source = new CancellationSource(); int finalCallbacks = 0;
        Assert.Throws<CancelledException>(() => NativeFiles.WriteNewFile(Destination, [1, 2], source.Token,
            count => { if (count == 2 && ++finalCallbacks == 2) source.RequestStop(); }));
        Assert.False(File.Exists(Destination)); NoStaging();
    }
    [Fact]
    public void Destination_created_during_write_is_not_overwritten()
    {
        Assert.Throws<IOException>(() => NativeFiles.WriteNewFile(Destination, [1, 2], progress:
            count => { if (count != 0 && !File.Exists(Destination)) File.WriteAllBytes(Destination, [9]); }));
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(Destination)); NoStaging();
    }
    [Fact]
    public async Task Concurrent_publishers_have_exactly_one_winner()
    {
        const int count = 8;
        using var barrier = new Barrier(count);
        var writers = Enumerable.Range(0, count).Select(index => Task.Factory.StartNew(() =>
        {
            int callbacks = 0;
            try
            {
                NativeFiles.WriteNewFile(Destination, [(byte)index], progress: written =>
                {
                    // All contenders have flushed and closed their staging files;
                    // none has published yet. The initial existence check cannot help.
                    if (written == 1 && ++callbacks == 2 && !barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("Publication barrier timed out");
                });
                return (Index: index, Error: (Exception?)null);
            }
            catch (Exception error) { return (Index: index, Error: error); }
        }, System.Threading.CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var results = await Task.WhenAll(writers);
        var winner = Assert.Single(results, result => result.Error is null);
        foreach (var loser in results.Where(result => result.Error is not null)) Assert.IsType<IOException>(loser.Error);
        Assert.Equal(new byte[] { (byte)winner.Index }, File.ReadAllBytes(Destination)); NoStaging();
    }

    [Fact]
    public void Callback_failure_retains_identity_and_cleans_staging()
    {
        var failure = new InvalidOperationException("authored callback failure");
        var caught = Assert.Throws<InvalidOperationException>(() => NativeFiles.WriteNewFile(Destination, [1], progress:
            count => { if (count != 0) throw failure; }));
        Assert.Same(failure, caught); Assert.False(File.Exists(Destination)); NoStaging();
    }
    [Fact]
    public void Empty_file_publishes_and_directory_destination_is_rejected()
    {
        NativeFiles.WriteNewFile(Destination, []); Assert.Empty(File.ReadAllBytes(Destination));
        Assert.Throws<IOException>(() => NativeFiles.WriteNewFile(directory, [])); NoStaging();
    }

    [Fact]
    public void New_output_identity_resolves_existing_prefix_without_creating_missing_directories()
    {
        string target = Path.Combine(directory, "target"); Directory.CreateDirectory(target);
        string expected = Path.Combine(NativeFiles.CanonicalNewPath(target), "missing", "nested", "new.json");
        Assert.Equal(expected, NativeFiles.CanonicalNewPath(Path.Combine(target, ".", "missing", "nested", "new.json")));
        Assert.False(Directory.Exists(Path.Combine(target, "missing")));
        if (!OperatingSystem.IsWindows())
        {
            // Unix directory links need no elevated link-creation privilege.
            string alias = Path.Combine(directory, "alias"); Directory.CreateSymbolicLink(alias, target);
            Assert.Equal(expected, NativeFiles.CanonicalNewPath(Path.Combine(alias, "missing", "nested", "new.json")));
            File.WriteAllBytes(Path.Combine(target, "existing.json"), [1]);
            Assert.Equal(NativeFiles.CanonicalNewPath(Path.Combine(target, "existing.json")), NativeFiles.CanonicalNewPath(Path.Combine(alias, "existing.json")));
        }
    }

    [Fact]
    public void Content_mount_reads_owned_bytes_with_limits_and_distinct_backing_identity()
    {
        File.WriteAllBytes(Destination, [1, 2, 3]);
        string second = Path.Combine(directory, "same-bytes.json"); File.WriteAllBytes(second, [1, 2, 3]);
        var mount = new ContentDirectory(directory);
        var first = mount.Read(Path.GetFileName(Destination), 3);
        Assert.Equal(new byte[] { 1, 2, 3 }, first.Bytes.ToArray());
        Assert.EndsWith("/" + Path.GetFileName(Destination), first.Identity);
        Assert.NotEqual(first.Identity, mount.Read("same-bytes.json").Identity);
        Assert.Equal(first.Identity, mount.Read(Path.GetFileName(Destination)).Identity);
        Assert.Throws<IOException>(() => mount.Read(Path.GetFileName(Destination), 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => mount.Read(Path.GetFileName(Destination), -1));
        File.WriteAllBytes(Destination, [9]); Assert.Equal(new byte[] { 1, 2, 3 }, first.Bytes.ToArray());
    }
    [Fact]
    public void Content_scope_requires_a_contained_regular_file()
    {
        Directory.CreateDirectory(Path.Combine(directory, "bundle"));
        File.WriteAllBytes(Path.Combine(directory, "bundle", "data.json"), [1]);
        File.WriteAllBytes(Path.Combine(directory, "outside.json"), [2]);
        var mount = new ContentDirectory(directory);
        Assert.Equal(new byte[] { 1 }, mount.Read("bundle/data.json", requiredDirectory: "bundle").Bytes.ToArray());
        Directory.CreateDirectory(Path.Combine(directory, "bundle", "nested é"));
        File.WriteAllBytes(Path.Combine(directory, "bundle", "nested é", "存档.json"), [3]);
        Assert.Equal(new byte[] { 3 }, mount.Read("bundle/nested é/存档.json", requiredDirectory: "bundle/nested é").Bytes.ToArray());
        Assert.Throws<ArgumentException>(() => mount.Read("bundle/data.json", requiredDirectory: "bundle/nested é"));
        Assert.Throws<ArgumentException>(() => mount.Read("outside.json", requiredDirectory: "bundle"));
        Assert.Throws<ArgumentException>(() => mount.Read("bundle"));
        Assert.Throws<ArgumentException>(() => new ContentDirectory(Path.Combine(directory, "outside.json")));
    }
    [Fact]
    public void Content_mount_rejects_paths_before_opening()
    {
        var mount = new ContentDirectory(directory);
        foreach (string path in new[] { "", "../data", "/absolute", "a//b", "./file", "a/", "a\\b", "a\nfile" })
            Assert.Throws<ArgumentException>(() => mount.Read(path));
    }
}
