using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Export.Sqlite;
using OpenCiv2.IO.Native;
using OpenCiv2.Query;

return Cli.Run(args, Export, ExportLoaded);

static byte[] Export(StoredSnapshot snapshot, string destination, bool json) =>
    WriteDatabase(destination, stop => SqliteSnapshot.Create(snapshot, stop), databaseSize =>
        Receipt("openciv2-analysis/1", snapshot.SourceHash, databaseSize, json));

static byte[] ExportLoaded(LocalPlayerSnapshot snapshot, string sourceHash, string destination, bool json) =>
    WriteDatabase(destination, stop => SqliteSnapshot.CreatePlayer(snapshot, sourceHash, stop), databaseSize =>
        Receipt("openciv2-player-analysis/1", sourceHash, databaseSize, json));

static byte[] WriteDatabase(string destination, Func<StopToken, byte[]> export, Func<int, byte[]> receipt)
{
    var cancellation = new CancellationSource();
    ConsoleCancelEventHandler interrupt = (_, e) =>
    {
        e.Cancel = true;
        cancellation.RequestStop();
    };
    Console.CancelKeyPress += interrupt;
    PosixSignalRegistration? terminate = null;
    try
    {
        if (!OperatingSystem.IsWindows())
        {
            terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                cancellation.RequestStop();
            });
        }

        byte[] bytes = export(cancellation.Token);
        NativeFiles.WriteNewFile(destination, bytes, cancellation.Token);
        return receipt(bytes.Length);
    }
    finally
    {
        terminate?.Dispose();
        Console.CancelKeyPress -= interrupt;
    }
}

static byte[] Receipt(string schema, string sourceHash, int bytes, bool json)
{
    string size = bytes.ToString(CultureInfo.InvariantCulture);
    if (json)
    {
        return Encoding.UTF8.GetBytes(new JsonObject
        {
            ["schema"] = "openciv2-export-receipt/1",
            ["complete"] = true,
            ["analysisSchema"] = schema,
            ["sourceSha256"] = sourceHash,
            ["bytes"] = size
        }.ToJsonString());
    }

    string text = schema == "openciv2-analysis/1"
        ? $"Exported SQLite analysis (schema 1); {size} bytes; source SHA-256 {sourceHash}\n"
          + "Validation coverage remains incomplete; inspect database diagnostics."
        : $"Exported SQLite analysis ({schema}), {size} bytes, source SHA-256 {sourceHash}.";
    return Encoding.UTF8.GetBytes(text);
}
