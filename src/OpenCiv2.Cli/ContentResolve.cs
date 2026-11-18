using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats.Content;
using OpenCiv2.IO.Native;

internal static partial class ContentCli
{
    internal static MountedPack Mount(string filename)
    {
        string path = Path.GetFullPath(filename); var directory = new ContentDirectory(Path.GetDirectoryName(path)!);
        ContentPaths.ValidateRelative(Path.GetFileName(path)); var source = directory.Read(Path.GetFileName(path));
        return new(new(source.Identity, source.Bytes), (scope, relative) =>
        {
            var file = directory.Read(scope.Length == 0 ? relative : scope + "/" + relative, 256 * 1024 * 1024, scope);
            return new(file.Identity, file.Bytes);
        });
    }
    private static JsonObject Identity(PackIdentity value) => new() { ["id"] = value.Id, ["revision"] = value.Revision, ["sha256"] = value.Sha256 };
    private static JsonArray Strings(IEnumerable<string> values) => new(values.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray());
    private static JsonObject PackReport(ResolvedPack pack)
    {
        var report = new JsonObject
        {
            ["format"] = ContentPacks.ResolverVersion,
            ["status"] = "resolved",
            ["scope"] = "pack-selection-and-gameplay",
            ["profile"] = pack.Profile,
            ["defaultLocale"] = pack.DefaultLocale,
            ["selectedLocale"] = pack.SelectedLocale
        };
        var manifests = new JsonArray(); report["manifests"] = manifests;
        foreach (var receipt in pack.Manifests)
        {
            var row = Identity(receipt.Pack);
            row["provenance"] = new JsonObject { ["authors"] = Strings(receipt.Provenance.Authors), ["license"] = receipt.Provenance.License, ["notes"] = Strings(receipt.Provenance.Notes) };
            manifests.Add(row);
        }
        var files = new JsonArray(); report["files"] = files;
        void File(string role, ResolvedFile? file, string locale = "")
        {
            var row = new JsonObject { ["role"] = role }; if (locale.Length != 0) row["locale"] = locale;
            if (file is null) row["externalWorld"] = true;
            else { row["owner"] = Identity(file.Owner); row["path"] = file.Receipt.Path; row["sha256"] = file.Receipt.Sha256; }
            files.Add(row);
        }
        foreach (var (name, document) in pack.Components) File(name, document.File);
        foreach (var (name, languages) in pack.Localized) foreach (var (locale, document) in languages) File(name, document.File, locale);
        if (pack.Compatibility is not null) File("compatibility", pack.Compatibility.File);
        foreach (var scenario in pack.Scenarios) File("scenario:" + scenario.Id, scenario.World);
        foreach (var resource in pack.Resources) File("resource:" + resource.Id, resource.File);
        var gameplay = pack.Gameplay;
        var receipts = new JsonArray(gameplay.Bundle.Files.Select(f => (JsonNode)new JsonObject { ["path"] = f.Path, ["sha256"] = f.Sha256 }).ToArray());
        report["gameplay"] = new JsonObject
        {
            ["owner"] = Identity(gameplay.Owner),
            ["builtin"] = gameplay.Manifest is null,
            ["semanticSha256"] = gameplay.Bundle.SemanticSha256,
            ["manifestSha256"] = gameplay.Bundle.ManifestSha256,
            ["classicValues"] = gameplay.Bundle.ClassicValues,
            ["mode"] = gameplay.Bundle.CustomMode ? "custom" : "classic",
            ["files"] = receipts
        };
        if (gameplay.Manifest is not null) File("gameplay", gameplay.Manifest);
        report["limitations"] = Strings(["Rule/text/event bodies, cross-references, world/media bytes, session and original-peer admission are not validated."]);
        return report;
    }
    private enum ContentValidation { Selection, Rules, Names, Preferences }
    private static int Resolve(string[] args, ContentValidation validation = ContentValidation.Selection)
    {
        bool validateRules = validation != ContentValidation.Selection;
        if (args.Length == 0) throw new ArgumentException("Usage: oc2 content resolve|validate-rules|validate-names|validate-preferences CONTENT_MANIFEST [--parent MANIFEST]... [--locale LOCALE] [--require-classic] [--json]");
        bool json = false, classic = false; string? locale = null; var parents = new List<string>();
        for (int i = 1; i < args.Length; ++i)
        {
            if (args[i] == "--json" && !json) json = true;
            else if (args[i] == "--require-classic" && !classic) classic = true;
            else if (args[i] == "--parent" && i + 1 < args.Length) parents.Add(args[++i]);
            else if (args[i] == "--locale" && locale is null && i + 1 < args.Length)
            { locale = args[++i]; if (locale.Length == 0) throw new ArgumentException("Locale must not be empty"); }
            else throw new ArgumentException("Unknown, repeated or incomplete content selection option");
        }
        string format = validation switch { ContentValidation.Rules => "openciv2-rules-validation/1", ContentValidation.Names => "openciv2-city-names-validation/1", ContentValidation.Preferences => "openciv2-city-preferences-validation/1", _ => ContentPacks.ResolverVersion };
        var report = new JsonObject { ["format"] = format }; int status = 0;
        try
        {
            var selected = Mount(args[0]); var mountedParents = parents.Select(Mount).ToArray();
            var pack = ContentPacks.Resolve(selected, mountedParents, locale ?? "", classic);
            JsonObject? rulesReport = null;
            CityPreferenceContent? preferences = null; CityNameContent? names = null;
            if (validateRules)
            {
                var rules = NativeRules.Load(pack); var state = new RuleState(); state.Text.Initialize();
                if (validation == ContentValidation.Preferences) preferences = NativeCityContent.LoadPreferences(pack, rules.Input.Definitions, TextComparison.AsciiNameEqual);
                if (validation == ContentValidation.Names) names = NativeCityContent.LoadNames(pack, rules.Input.Definitions, TextComparison.AsciiNameEqual);
                new RuleInstaller(rules.Input, state).Load(true); var definitions = state.Definitions;
                rulesReport = new JsonObject
                {
                    ["enabledAdvances"] = definitions.EnabledAdvanceCount,
                    ["roadMovementMultiplier"] = definitions.Cosmic[CosmicParameter.RoadMovementMultiplier],
                    ["foodBoxRows"] = definitions.Cosmic[CosmicParameter.FoodBoxRows],
                    ["shieldBoxRows"] = definitions.Cosmic[CosmicParameter.ShieldBoxRows],
                    ["governmentAdvances"] = new JsonArray(rules.Input.GovernmentAdvances.Select(a => a is null ? null : (JsonNode)JsonValue.Create((int)a.Value)!).ToArray()),
                    ["units"] = new JsonArray(definitions.Units.Select(u => (JsonNode)new JsonObject { ["id"] = u.Identity.Id, ["movement"] = u.Movement, ["hitPoints"] = u.HitPoints, ["costRows"] = u.CostRows }).ToArray())
                };
            }
            report = PackReport(pack); report["format"] = format;
            if (validateRules)
            {
                report["status"] = "validated"; report["scope"] = "rules-and-gameplay-references"; report["rules"] = rulesReport;
                report["limitations"] = Strings(["City names/preferences, text/events, compatibility metadata, world/media bytes, session and original-peer admission are not validated."]);
            }
            if (preferences is not null)
            {
                report["scope"] = "rules-gameplay-and-city-preferences";
                report["cityPreferences"] = new JsonObject
                {
                    ["humanCityDefensePriority"] = preferences.Update.DeprioritizeDefense ? "deprioritize" : "unchanged",
                    ["buildSlots"] = new JsonArray(preferences.Update.BuildOrder.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray()),
                    ["comparison"] = "ascii-with-exact-byte-equality"
                };
                report["limitations"] = Strings(["Unestablished non-ASCII name comparisons reject. Full classic export representability, city names, text/events, compatibility metadata, world/media bytes, session and original-peer admission are not validated."]);
            }
            if (names is not null)
            {
                report["scope"] = "rules-gameplay-and-city-names"; var lists = names.Catalog.Layers[0];
                report["cityNames"] = new JsonObject
                {
                    ["comparison"] = "ascii-with-exact-byte-equality",
                    ["startingListsComplete"] = names.StartingListsComplete,
                    ["fallbackAvailable"] = lists.Fallback is not null,
                    ["fallbackCount"] = lists.Fallback?.Count ?? 0,
                    ["civilizations"] = new JsonArray(lists.Civilizations.Select(l => (JsonNode)new JsonObject { ["pluralName"] = l.Plural.Unicode, ["nameCount"] = l.Names.Count }).ToArray())
                };
                report["limitations"] = Strings(["Unestablished non-ASCII comparisons reject. Starting-list coverage is required for base packs and reported for overlays. Full classic export representability, preferences, text/events, world/media bytes, session and original-peer admission are not validated."]);
            }
            string summary = validation switch { ContentValidation.Names => "Validated city names and rules for", ContentValidation.Preferences => "Validated city preferences and rules for", ContentValidation.Rules => "Validated rule definitions for", _ => "Resolved content pack" };
            if (!json) Cli.Write($"{summary} {pack.Manifests[^1].Pack.Id} revision {pack.Manifests[^1].Pack.Revision}\nLocale: {pack.SelectedLocale}; manifests: {pack.Manifests.Count}\n{report["limitations"]![0]!.GetValue<string>()}\n");
        }
        catch (ContentException error)
        {
            status = error.Code == "invalid-json" ? 4 : error.Code == "source-error" ? 2 : 1;
            report["status"] = "rejected"; report["code"] = error.Code; report["location"] = error.Location; report["message"] = error.Message;
            if (!json) Console.Error.WriteLine(error.Code + ": " + error.Message);
        }
        catch (Exception error)
        {
            status = 2; report["status"] = "rejected"; report["code"] = "input-error"; report["message"] = error.Message;
            if (!json) Console.Error.WriteLine(error.Message);
        }
        if (json) Cli.Write(report.ToJsonString() + "\n");
        return status;
    }
}
