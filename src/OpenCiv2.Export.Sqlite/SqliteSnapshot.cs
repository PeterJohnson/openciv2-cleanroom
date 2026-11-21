using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using OpenCiv2.Base;
using OpenCiv2.Query;

namespace OpenCiv2.Export.Sqlite;

/// <summary>M1 analysis schema 1. Owns a private in-memory transaction; no filesystem or live-world access.</summary>
public static partial class SqliteSnapshot
{
    public const string RequiredVersion = "3.54.1";
    public static string NativeVersion =>
        Marshal.PtrToStringUTF8(NativeSqlite.Version())
            ?? throw new InvalidOperationException("SQLite version is unavailable");

    private static void Check(DatabaseHandle database, int code)
    {
        if (code != 0)
        {
            throw new InvalidOperationException(
                "SQLite export: " + Marshal.PtrToStringUTF8(NativeSqlite.Error(database)));
        }
    }

    private sealed class Statement : IDisposable
    {
        private readonly DatabaseHandle database;
        private readonly StatementHandle handle;

        internal Statement(DatabaseHandle database, string sql)
        {
            this.database = database;
            int result = NativeSqlite.Prepare(database, sql, -1, out nint pointer, 0);
            handle = new(pointer);
            try
            {
                Check(database, result);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        internal void Bind(int index, string text) =>
            Check(database, NativeSqlite.Text(handle, index, text, (ulong)Encoding.UTF8.GetByteCount(text), -1, 1));

        internal void Bind(int index, long value) => Check(database, NativeSqlite.Integer(handle, index, value));

        internal unsafe void Bind(int index, ReadOnlySpan<byte> bytes)
        {
            byte empty = 0;
            fixed (byte* data = bytes)
            {
                Check(
                    database,
                    NativeSqlite.Blob(handle, index, bytes.IsEmpty ? &empty : data, (ulong)bytes.Length, -1));
            }
        }

        internal void Value(FieldValue? value)
        {
            switch (value)
            {
                case null:
                    Bind(7, "null");
                    break;
                case FieldValue.Bytes bytes:
                    Bind(7, "bytes");
                    Bind(9, bytes.Value.ToArray());
                    break;
                case FieldValue.Signed integer:
                    Bind(7, "signed");
                    Bind(8, integer.Value.ToString(CultureInfo.InvariantCulture));
                    break;
                case FieldValue.Unsigned integer:
                    Bind(7, "unsigned");
                    Bind(8, integer.Value.ToString(CultureInfo.InvariantCulture));
                    break;
                default:
                    throw new ArgumentException("Unknown stored field kind");
            }
        }

        internal void Run()
        {
            int result = NativeSqlite.Step(handle);
            if (result != 101)
            {
                Check(database, result);
            }

            Check(database, NativeSqlite.Reset(handle));
            Check(database, NativeSqlite.Clear(handle));
        }

        public void Dispose() => handle.Dispose();
    }

    /// <summary>Returns a complete caller-owned database image. Progress runs only between completed records and at
    /// completion boundaries.</summary>
    public static byte[] Create(StoredSnapshot snapshot, StopToken stop = default, Action<int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.Decoded)
        {
            throw new ArgumentException("SQLite export requires a decoded stored snapshot", nameof(snapshot));
        }

        return CreateImage(Schema, snapshot.Records.Count, (database, checkpoint) =>
            StoreSnapshot(database, checkpoint, snapshot), stop, progress);
    }

    private static void StoreSnapshot(DatabaseHandle database, Action<int> checkpoint, StoredSnapshot snapshot)
    {
        string identity = snapshot.SourceHash;
        using var header = new Statement(
            database,
            "INSERT INTO snapshots VALUES(?,?,?,?, 'stored', 'offline-omniscient',?,0)");
        header.Bind(1, identity);
        header.Bind(2, snapshot.Kind == FileKind.Save ? "classic-save" : "classic-raw-map");
        if (snapshot.Version is { } version)
        {
            header.Bind(3, version);
        }

        header.Bind(4, snapshot.SourceBytes.Count);
        header.Bind(5, snapshot.HasErrors ? "errors" : "passed-available-checks");
        header.Run();
        using var metadata = new Statement(database, "INSERT INTO metadata VALUES('sqlite_version',?)");
        metadata.Bind(1, NativeVersion);
        metadata.Run();
        using var recordInsert = new Statement(database, "INSERT INTO records VALUES(?,?,?,?,?,?,'stored')");
        using var fieldInsert = new Statement(database, "INSERT INTO fields VALUES(?,?,?,?,?,?,?,?,?)");
        var counts = new SortedDictionary<Section, int>();
        for (int i = 0; i < snapshot.Records.Count; ++i)
        {
            var record = snapshot.Records[i];
            counts.TryGetValue(record.Key.Section, out int count);
            counts[record.Key.Section] = count + 1;
            recordInsert.Bind(1, identity);
            recordInsert.Bind(2, record.Key.Section.Name());
            recordInsert.Bind(3, record.Key.Slot);
            recordInsert.Bind(4, i);
            recordInsert.Bind(5, record.Offset);
            recordInsert.Bind(6, record.Bytes.ToArray());
            recordInsert.Run();
            for (int j = 0; j < record.Fields.Count; ++j)
            {
                var field = record.Fields[j];
                fieldInsert.Bind(1, identity);
                fieldInsert.Bind(2, record.Key.Section.Name());
                fieldInsert.Bind(3, record.Key.Slot);
                fieldInsert.Bind(4, j);
                fieldInsert.Bind(5, field.Name);
                fieldInsert.Bind(6, field.Type);
                fieldInsert.Value(field.Value);
                fieldInsert.Run();
            }
            checkpoint(i + 1);
        }
        using var coverage = new Statement(database, "INSERT INTO coverage VALUES(?,?,?,1)");
        foreach (var (section, count) in counts)
        {
            coverage.Bind(1, identity);
            coverage.Bind(2, section.Name());
            coverage.Bind(3, count);
            coverage.Run();
        }
        using var checks = new Statement(database, "INSERT INTO validation_checks VALUES(?,?,?)");
        for (int i = 0; i < snapshot.Checks.Count; ++i)
        {
            checks.Bind(1, identity);
            checks.Bind(2, i);
            checks.Bind(3, snapshot.Checks[i]);
            checks.Run();
        }
        using var diagnostics = new Statement(database, "INSERT INTO diagnostics VALUES(?,?,?,?,?,?,?,?)");
        string[] categories = [
            "structural",
            "compatibility",
            "semantic"
        ], severities = [
            "information",
            "warning",
            "error"
        ];
        for (int i = 0; i < snapshot.Diagnostics.Count; ++i)
        {
            var value = snapshot.Diagnostics[i];
            diagnostics.Bind(1, identity);
            diagnostics.Bind(2, i);
            diagnostics.Bind(3, categories[(int)value.Category]);
            diagnostics.Bind(4, severities[(int)value.Severity]);
            diagnostics.Bind(5, value.Code);
            diagnostics.Bind(6, value.Field);
            if (value.Offset is { } offset)
            {
                diagnostics.Bind(7, offset);
            }

            diagnostics.Bind(8, value.Message);
            diagnostics.Run();
        }
    }

    private static byte[] CreateImage(string schema, int records, Action<DatabaseHandle, Action<int>> populate,
        StopToken stop, Action<int>? progress = null)
    {
        void Checkpoint(int completed)
        {
            stop.CheckStop();
            progress?.Invoke(completed);
            stop.CheckStop();
        }
        Checkpoint(0);
        if (NativeVersion != RequiredVersion || NativeSqlite.CompileOption("OMIT_LOAD_EXTENSION") != 1)
        {
            throw new InvalidOperationException(
                "SQLite export requires pinned SQLite " + RequiredVersion + " with extension loading disabled");
        }

        int opened = NativeSqlite.Open(":memory:", out nint pointer, 2 | 4 | 0x80, 0);
        using var database = new DatabaseHandle(pointer);
        if (database.IsInvalid)
        {
            throw new OutOfMemoryException("SQLite could not allocate a database connection");
        }

        Check(database, opened);
        Check(database, NativeSqlite.Execute(database, schema, 0, 0, 0));
        populate(database, Checkpoint);
        Checkpoint(records);
        Check(database, NativeSqlite.Execute(database, "COMMIT", 0, 0, 0));
        nint image = NativeSqlite.Serialize(database, "main", out long length, 0);
        try
        {
            if (image == 0 || length <= 0)
            {
                throw new InvalidOperationException("SQLite export serialization failed");
            }

            if (length > Array.MaxLength)
            {
                throw new InvalidOperationException("SQLite export exceeds the managed byte-array domain");
            }

            Checkpoint(records);
            var result = new byte[(int)length];
            Marshal.Copy(image, result, 0, result.Length);
            return result;
        }
        finally
        {
            NativeSqlite.Free(image);
        }
    }
}
