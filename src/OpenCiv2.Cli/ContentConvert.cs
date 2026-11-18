using System.Text.Json.Nodes;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Formats.Content;
using OpenCiv2.IO.Native;

internal static partial class ContentCli
{
    private static ContentSource ConversionSource(string path)
    {
        var source = new ContentDirectory(Path.GetDirectoryName(path)!).Read(Path.GetFileName(path));
        return new(source.Identity, source.Bytes);
    }
    private static void NewOutput(string path)
    {
        try { File.GetAttributes(path); }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return; }
        throw new IOException("Conversion requires new outputs distinct from the source");
    }
    private static int ConvertRules(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Usage: oc2 content convert-rules RULES_SOURCE NEW_RULES NEW_COMPATIBILITY");
        var report = new JsonObject { ["format"] = "openciv2-content-conversion/1" }; int status = 0; bool publishedRules = false;
        try
        {
            string input = Path.GetFullPath(args[0]), rulesPath = Path.GetFullPath(args[1]), mappingPath = Path.GetFullPath(args[2]);
            NewOutput(rulesPath); NewOutput(mappingPath);
            string sourcePath = NativeFiles.CanonicalNewPath(input), rulesTarget = NativeFiles.CanonicalNewPath(rulesPath), mappingTarget = NativeFiles.CanonicalNewPath(mappingPath);
            if (sourcePath == rulesTarget || sourcePath == mappingTarget) throw new IOException("Conversion requires new outputs distinct from the source");
            if (rulesTarget == mappingTarget) throw new IOException("Rules and compatibility require distinct outputs");
            var converted = ContentConversions.Rules(ConversionSource(input), TextCase.Ascii); report = JsonNode.Parse(converted.Receipt.GetRawText())!.AsObject();
            void Publish(string role, string path, byte[] bytes)
            {
                report["outputs"]![role]!["path"] = path.Replace(Path.DirectorySeparatorChar, '/'); report["outputs"]![role]!["published"] = false;
                NativeFiles.WriteNewFile(path, bytes); report["outputs"]![role]!["published"] = true;
            }
            Publish("rules", rulesPath, converted.Rules.ToArray()); publishedRules = true;
            Publish("compatibility", mappingPath, converted.Compatibility.ToArray()); report["status"] = "converted";
        }
        catch (ContentException error)
        {
            status = error.Code == "source-error" ? 2 : error.Code == "invalid-json" ? 4 : 1;
            report["status"] = "rejected"; report["code"] = error.Code; report["location"] = error.Location; report["message"] = error.Message;
        }
        catch (Exception error)
        {
            status = 2; report["status"] = publishedRules ? "partial" : "rejected"; report["code"] = "input-output-error"; report["message"] = error.Message;
        }
        Cli.Write(report.ToJsonString() + "\n"); return status;
    }
    private static int ConvertCity(string[] args, bool cityNames)
    {
        if (args.Length < 3) throw new ArgumentException("Usage: oc2 content convert-names|convert-preferences SOURCE CONTENT_MANIFEST NEW_OUTPUT [--parent MANIFEST]...");
        var parents = new List<string>();
        for (int i = 3; i < args.Length; ++i)
            if (args[i] == "--parent" && i + 1 < args.Length) parents.Add(args[++i]);
            else throw new ArgumentException("Unknown or incomplete conversion option");
        var report = new JsonObject { ["format"] = "openciv2-content-conversion/1" }; int status = 0;
        try
        {
            string input = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[2]); NewOutput(output);
            if (NativeFiles.CanonicalNewPath(input) == NativeFiles.CanonicalNewPath(output)) throw new IOException("Conversion requires a new output distinct from its source");
            var source = ConversionSource(input); var pack = ContentPacks.Resolve(Mount(args[1]), parents.Select(Mount).ToArray());
            var converted = cityNames ? ContentConversions.CityNames(source, pack, TextCase.Ascii) : ContentConversions.CityPreferences(source, pack, TextCase.Ascii);
            report = JsonNode.Parse(converted.Receipt.GetRawText())!.AsObject(); report["output"]!["path"] = output.Replace(Path.DirectorySeparatorChar, '/');
            report["selectedPacks"] = new JsonArray(pack.Manifests.Select(p => { var row = Identity(p.Pack); row["source"] = p.SourceIdentity; return (JsonNode)row; }).ToArray());
            NativeFiles.WriteNewFile(output, converted.Component.ToArray()); report["status"] = "converted";
        }
        catch (ContentException error)
        {
            status = error.Code == "source-error" ? 2 : error.Code == "invalid-json" ? 4 : 1;
            report["status"] = "rejected"; report["code"] = error.Code; report["location"] = error.Location; report["message"] = error.Message;
        }
        catch (Exception error) { status = 2; report["status"] = "rejected"; report["code"] = "input-output-error"; report["message"] = error.Message; }
        Cli.Write(report.ToJsonString() + "\n"); return status;
    }
}
