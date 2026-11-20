using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Export.Sqlite;
using OpenCiv2.IO.Native;
using OpenCiv2.Query;

return Cli.Run(args, Export);

static byte[] Export(StoredSnapshot snapshot, string destination, bool json)
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

        byte[] bytes = SqliteSnapshot.Create(snapshot, cancellation.Token);
        NativeFiles.WriteNewFile(destination, bytes, cancellation.Token);
        string size = bytes.Length.ToString(CultureInfo.InvariantCulture);
        if (json)
        {
            return Encoding.UTF8.GetBytes(new JsonObject
            {
                ["schema"] = "openciv2-export-receipt/1",
                ["complete"] = true,
                ["analysisSchema"] = "openciv2-analysis/1",
                ["sourceSha256"] = snapshot.SourceHash,
                ["bytes"] = size
            }.ToJsonString());
        }

        return Encoding.UTF8.GetBytes(
            "Exported SQLite analysis (schema 1); " + size + " bytes; source SHA-256 " + snapshot.SourceHash + "\nValidation coverage remains incomplete; inspect database diagnostics.");
    }
    finally
    {
        terminate?.Dispose();
        Console.CancelKeyPress -= interrupt;
    }
}
