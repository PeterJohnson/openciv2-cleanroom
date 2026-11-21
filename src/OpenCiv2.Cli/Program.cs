using System.Text;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.IO.Native;
using OpenCiv2.Query;
using QueryAccess = OpenCiv2.Query.FileAccess;

return Cli.Run(args);

public static class Cli
{
    private const string Usage = """
Usage: oc2 inspect FILE [--json] [--cities|--units|--tiles|--civilizations|--section NAME]
       oc2 validate FILE [--json]
       oc2 diff BEFORE AFTER [--json]
       oc2 export FILE --sqlite NEW_DATABASE [--json] (optional build feature)
       oc2 content validate GAMEPLAY_MANIFEST [--require-classic] [--json]
       oc2 content resolve CONTENT_MANIFEST [--parent MANIFEST]... [--locale LOCALE] [--require-classic] [--json]
       oc2 content validate-rules|validate-names|validate-preferences CONTENT_MANIFEST [--parent MANIFEST]... [--locale LOCALE] [--require-classic] [--json]
       oc2 content convert-rules RULES_SOURCE NEW_RULES NEW_COMPATIBILITY
       oc2 content convert-names|convert-preferences SOURCE CONTENT_MANIFEST NEW_OUTPUT [--parent MANIFEST]...
Options: --format save|map (default: .mp means map; all others mean save)
         --view stored (default)
         --view loaded --rules alder-valley (inspect or optional SQLite export)
Stored save support: version 0x2c. Inputs are read-only.
Exit: 0 checks passed/equal; 1 semantic errors/different; 2 usage/I/O failure;
      3 unsupported feature/version/view/comparison; 4 malformed input; 130 cancelled.
""" + "\n";

    internal static void Write(byte[] bytes)
    {
        try
        {
            using Stream output = StandardOutput.Open();
            output.Write(bytes);
            output.Flush();
        }
        catch (IOException error)
        {
            throw new IOException("Output write failed", error);
        }
    }

    internal static void Write(string text) => Write(Encoding.UTF8.GetBytes(text));

    private static FileKind Kind(string path, FileKind? selected) => selected ??
        (Path.GetExtension(path).Equals(".mp", StringComparison.OrdinalIgnoreCase) ? FileKind.RawMap : FileKind.Save);

    private static int Status(StoredSnapshot s) => s.Unsupported ? 3 : !s.Decoded ? 4 : s.HasErrors ? 1 : 0;

    public static int Run(string[] args, Func<StoredSnapshot, string, bool, byte[]>? export = null,
        Func<LocalPlayerSnapshot, string, string, bool, byte[]>? exportLoaded = null)
    {
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "help")
            {
                Write(Usage);
                return args.Length == 0 ? 2 : 0;
            }
            string command = args[0];
            if (command == "content")
            {
                return ContentCli.Run(args[1..]);
            }

            if (command is not ("inspect" or "validate" or "diff" or "export"))
            {
                throw new ArgumentException("Unknown command");
            }

            bool json = false, positionalOnly = false;
            var paths = new List<string>();
            var selection = new List<Section>();
            FileKind? format = null;
            string? destination = null, loadRules = null;
            bool loaded = false;
            for (int i = 1; i < args.Length; ++i)
            {
                string arg = args[i];
                if (positionalOnly || !arg.StartsWith("--", StringComparison.Ordinal))
                {
                    paths.Add(arg);
                    continue;
                }
                if (arg == "--")
                {
                    positionalOnly = true;
                    continue;
                }
                if (arg == "--json")
                {
                    json = true;
                    continue;
                }
                if (arg == "--help")
                {
                    Write(Usage);
                    return 0;
                }
                string Next() => ++i < args.Length ? args[i] : throw new ArgumentException("Missing option value");
                if (arg == "--sqlite")
                {
                    if (command != "export" || destination is not null)
                    {
                        throw new ArgumentException("--sqlite is valid once for export only");
                    }

                    destination = Next();
                    continue;
                }
                if (arg == "--view")
                {
                    string view = Next();
                    if (view is not ("stored" or "loaded"))
                    {
                        Console.Error.WriteLine("Available views are stored and the Alder Valley loaded player view");
                        return 3;
                    }

                    loaded = view == "loaded";
                    continue;
                }
                if (arg == "--rules")
                {
                    loadRules = Next();
                    continue;
                }
                if (arg == "--format")
                {
                    format = Next() switch
                    {
                        "save" => FileKind.Save,
                        "map" => FileKind.RawMap,
                        _ => throw new ArgumentException(
                            "Format must be save or map")
                    };
                    continue;
                }
                string name = arg == "--section" ? Next() : arg[2..];
                selection.Add(Sections.Parse(name) ?? throw new ArgumentException("Unknown option or section"));
            }
            if (paths.Count != (command == "diff" ? 2 : 1))
            {
                throw new ArgumentException("Incorrect number of input paths");
            }

            if (command != "inspect" && selection.Count != 0)
            {
                throw new ArgumentException("Section selection is available only for inspect");
            }

            if (loaded)
            {
                if (loadRules != "alder-valley" || Kind(paths[0], format) != FileKind.Save
                    || command is not ("inspect" or "export") || selection.Count != 0)
                {
                    Console.Error.WriteLine("Loaded inspection requires a save, --rules alder-valley and no stored sections");
                    return 3;
                }

                if (command == "export" && (exportLoaded is null || destination is null))
                {
                    Console.Error.WriteLine("Loaded export requires the SQLite build and --sqlite NEW_DATABASE");
                    return 3;
                }

                return LoadedView.Run(paths[0], json, command == "export"
                    ? (snapshot, hash) => exportLoaded!(snapshot, hash, destination!, json) : null);
            }

            if (loadRules is not null)
            {
                throw new ArgumentException("--rules applies only to --view loaded");
            }

            if (command == "export")
            {
                if (destination is null)
                {
                    throw new ArgumentException("Export requires --sqlite NEW_DATABASE");
                }

                if (export is null)
                {
                    Console.Error.WriteLine("SQLite export is unavailable in this build");
                    return 3;
                }
            }
            var before = StoredInspection.Inspect(
                NativeFiles.ReadFile(paths[0]),
                Kind(paths[0], format),
                QueryAccess.OfflineOmniscient);
            int result = Status(before);
            byte[] output;
            if (command == "export")
            {
                output = before.Decoded
                    ? export!(before, destination!, json)
                    : json
                        ? Output.SnapshotJson(before, includeRecords: false)
                        : Encoding.UTF8.GetBytes(Output.SnapshotText(before));
            }
            else if (command == "diff")
            {
                var after = StoredInspection.Inspect(
                    NativeFiles.ReadFile(paths[1]),
                    Kind(paths[1], format),
                    QueryAccess.OfflineOmniscient);
                var diff = StoredInspection.Diff(before, after);
                output = json
                    ? Output.DiffJson(before, after, diff)
                    : Encoding.UTF8.GetBytes(Output.DiffText(before, after, diff));
                result = Math.Max(result, Status(after));
                if (result < 3 && !diff.FieldsCompared)
                {
                    result = 3;
                }

                if (result == 0 && diff.Different)
                {
                    result = 1;
                }
            }
            else
            {
                output = json ? Output.SnapshotJson(before, selection, command == "inspect") :
                                Encoding.UTF8.GetBytes(
                                    Output.SnapshotText(
                                        before,
                                        selection,
                                        command == "inspect" && selection.Count != 0));
            }

            Write([.. output, (byte)'\n']);
            return result;
        }
        catch (CancelledException error)
        {
            Console.Error.WriteLine(error.Message);
            return 130;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            Console.Error.Write(Usage);
            return 2;
        }
    }
}
