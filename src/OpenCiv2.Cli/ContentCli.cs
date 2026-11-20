using System.Text.Json.Nodes;
using OpenCiv2.Formats.Content;
using OpenCiv2.IO.Native;

internal static partial class ContentCli
{
    public static int Run(string[] args)
    {
        if (args.Length != 0 && args[0] == "convert-rules")
        {
            return ConvertRules(args[1..]);
        }

        if (args.Length != 0 && args[0] is "convert-names" or "convert-preferences")
        {
            return ConvertCity(args[1..], args[0] == "convert-names");
        }

        if (args.Length != 0 && args[0] == "resolve")
        {
            return Resolve(args[1..]);
        }

        if (args.Length != 0 && args[0] == "validate-rules")
        {
            return Resolve(args[1..], ContentValidation.Rules);
        }

        if (args.Length != 0 && args[0] == "validate-names")
        {
            return Resolve(args[1..], ContentValidation.Names);
        }

        if (args.Length != 0 && args[0] == "validate-preferences")
        {
            return Resolve(args[1..], ContentValidation.Preferences);
        }

        if (args.Length < 2 || args[0] != "validate")
        {
            throw new ArgumentException("Usage: oc2 content validate GAMEPLAY_MANIFEST [--require-classic] [--json]");
        }

        bool json = false, requireClassic = false;
        for (int i = 2; i < args.Length; ++i)
        {
            if (args[i] == "--json" && !json)
            {
                json = true;
            }
            else if (args[i] == "--require-classic" && !requireClassic)
            {
                requireClassic = true;
            }
            else
            {
                throw new ArgumentException("Unknown or repeated content option");
            }
        }
        var report = new JsonObject { ["format"] = "openciv2-gameplay-validation/1" };
        int status = 0;
        try
        {
            string manifest = Path.GetFullPath(args[1]);
            var directory = new ContentDirectory(Path.GetDirectoryName(manifest)!);
            ContentPaths.ValidateRelative(Path.GetFileName(manifest));
            var source = directory.Read(Path.GetFileName(manifest));
            var bundle = GameplayContent.Load(source.Bytes.ToArray(), path =>
            {
                var value = directory.Read(path);
                return new(value.Identity, value.Bytes);
            }, requireClassic);
            const string limitation = "Selected rule references, mechanic bindings, session/checkpoint and original-peer admission are not checked.";
            report["status"] = "validated";
            report["scope"] = "gameplay-bundle-only";
            report["id"] = bundle.Id;
            report["revision"] = bundle.Revision;
            report["mode"] = bundle.CustomMode ? "custom" : "classic";
            report["classicValues"] = bundle.ClassicValues;
            report["manifestSha256"] = bundle.ManifestSha256;
            report["semanticSha256"] = bundle.SemanticSha256;
            report["files"] = new JsonArray(
                bundle.Files.Select(f => (JsonNode)new JsonObject { ["path"] = f.Path, ["sha256"] = f.Sha256 })
                    .ToArray());
            report["limitations"] = new JsonArray(JsonValue.Create(limitation));
            if (!json)
            {
                Cli.Write(
                    $"Validated gameplay bundle {bundle.Id} revision {bundle.Revision}\nMode: {(bundle.CustomMode ? "custom" : "classic")}; classic operands: {(bundle.ClassicValues ? "yes" : "no")}\nSemantic SHA-256: {bundle.SemanticSha256}\n{limitation}\n");
            }
        }
        catch (ContentException error)
        {
            status = error.Code == "invalid-json" ? 4 : error.Code == "source-error" ? 2 : 1;
            report["status"] = "rejected";
            report["code"] = error.Code;
            report["location"] = error.Location;
            report["message"] = error.Message;
            if (!json)
            {
                Console.Error.WriteLine(error.Code + ": " + error.Message);
            }
        }
        catch (Exception error)
        {
            status = 2;
            report["status"] = "rejected";
            report["code"] = "input-error";
            report["message"] = error.Message;
            if (!json)
            {
                Console.Error.WriteLine(error.Message);
            }
        }
        if (json)
        {
            Cli.Write(report.ToJsonString() + "\n");
        }

        return status;
    }
}
