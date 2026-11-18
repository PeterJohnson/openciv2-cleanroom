using OpenCiv2.Base;
using OpenCiv2.Export.Sqlite;
using OpenCiv2.Query;
using Xunit;
using Access = OpenCiv2.Query.FileAccess;

namespace OpenCiv2.Sqlite.Tests;

public sealed class ExportTests
{
    // F06 raw-map layout, authored independently of the exporter. Four ocean tiles.
    private static StoredSnapshot Snapshot()
    {
        byte[] bytes = [4, 0, 2, 0, 4, 0, 0, 0, 24, 0, 1, 0, 1, 0, .. Enumerable.Repeat((byte)255, 84),
            10, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0];
        var snapshot = StoredInspection.Inspect(bytes, FileKind.RawMap, Access.OfflineOmniscient);
        Assert.True(snapshot.Decoded); return snapshot;
    }
    [Fact]
    public void Pinned_native_library_emits_an_owned_complete_image_with_ordered_progress()
    {
        var snapshot = Snapshot(); var source = snapshot.SourceBytes; var records = snapshot.Records; var progress = new List<int>();
        byte[] image = SqliteSnapshot.Create(snapshot, progress: progress.Add);
        Assert.Equal("3.54.1", SqliteSnapshot.NativeVersion); Assert.True(image.Length > 4096); Assert.Equal("SQLite format 3\0"u8.ToArray(), image[..16]);
        Assert.Equal(Enumerable.Range(0, records.Count + 1).Concat([records.Count, records.Count]), progress);
        Assert.Equal(source, snapshot.SourceBytes); Assert.Equal(records, snapshot.Records); Assert.Equal(image, SqliteSnapshot.Create(snapshot));
        image[0] = 0; Assert.Equal((byte)'S', SqliteSnapshot.Create(snapshot)[0]);
    }
    [Fact]
    public void Cancellation_at_record_and_serialization_boundaries_discards_the_private_image()
    {
        var snapshot = Snapshot(); var expected = SqliteSnapshot.Create(snapshot);
        foreach (int occurrence in new[] { 0, 1, 2, 3 })
        {
            var stop = new CancellationSource(); int atEnd = 0, observed = -1;
            Assert.Throws<CancelledException>(() => SqliteSnapshot.Create(snapshot, stop.Token, completed =>
            {
                observed = completed;
                if (occurrence == 0 ? completed == 2 : completed == snapshot.Records.Count && ++atEnd == occurrence) stop.RequestStop();
            }));
            Assert.Equal(occurrence == 0 ? 2 : snapshot.Records.Count, observed); Assert.Equal(expected, SqliteSnapshot.Create(snapshot));
        }
    }
    [Fact]
    public void Precancelled_export_never_invokes_progress_and_invalid_input_rejects_first()
    {
        var stop = new CancellationSource(); stop.RequestStop(); bool called = false;
        Assert.Throws<CancelledException>(() => SqliteSnapshot.Create(Snapshot(), stop.Token, _ => called = true)); Assert.False(called);
        var invalid = StoredInspection.Inspect([], FileKind.Save, Access.OfflineOmniscient);
        Assert.Throws<ArgumentException>(() => SqliteSnapshot.Create(invalid, stop.Token, _ => called = true)); Assert.False(called);
    }
    [Fact]
    public void Callback_failure_preserves_identity_and_releases_private_resources()
    {
        var snapshot = Snapshot(); var expected = SqliteSnapshot.Create(snapshot); var source = snapshot.SourceBytes; var records = snapshot.Records;
        var failure = new InvalidOperationException("authored export failure");
        for (int i = 0; i < 16; ++i)
        {
            var caught = Assert.Throws<InvalidOperationException>(() => SqliteSnapshot.Create(snapshot, progress: completed => { if (completed == 2) throw failure; }));
            Assert.Same(failure, caught);
        }
        Assert.Equal(source, snapshot.SourceBytes); Assert.Equal(records, snapshot.Records); Assert.Equal(expected, SqliteSnapshot.Create(snapshot));
    }
}
