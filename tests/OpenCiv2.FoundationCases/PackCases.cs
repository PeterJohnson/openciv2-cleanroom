using System.Text;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class PackInput
    {
        public JsonObject Manifest { get; }
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public List<string> Reads { get; } = [];
        public List<string> Scopes { get; } = [];
        public PackInput(string name = "authored.root", bool root = true)
        {
            Manifest = new()
            {
                ["format"] = "openciv2-content-pack/1",
                ["id"] = name,
                ["revision"] = 1,
                ["profile"] = GameplayContent.Target,
                ["defaultLocale"] = "en",
                ["base"] = null,
                ["files"] = new JsonObject(),
                ["provenance"] = new JsonObject { ["authors"] = new JsonArray(JsonValue.Create("Independent fixture author")), ["license"] = "MIT", ["notes"] = new JsonArray() }
            };
            if (!root) return;
            Manifest["compatibility"] = null; Manifest["scenarios"] = new JsonArray(); Manifest["resources"] = new JsonArray();
            Manifest["gameplay"] = new JsonObject { ["builtin"] = GameplayContent.Target, ["revision"] = 1 };
            foreach (string kind in ContentPacks.PlainComponents) Document(kind, "", kind + ".json", "root");
            foreach (string kind in ContentPacks.LocalizedComponents) Document(kind, "en", kind + ".en.json", "root");
        }
        public JsonObject Source(string path, byte[] bytes)
        {
            Files[path] = bytes; return new() { ["path"] = path, ["sha256"] = ContentIdentity.Sha256(bytes) };
        }
        public JsonObject Source(string path, string text) => Source(path, Encoding.UTF8.GetBytes(text));
        public void Document(string component, string locale, string path, string marker)
        {
            var value = new JsonObject { ["format"] = ContentPacks.ComponentFormat(component), ["marker"] = marker };
            if (locale.Length != 0) value["locale"] = locale;
            var descriptor = Source(path, value.ToJsonString());
            if (locale.Length == 0) Manifest["files"]![component] = descriptor;
            else
            {
                if (Manifest["files"]![component] is null) Manifest["files"]![component] = new JsonObject();
                Manifest["files"]![component]![locale] = descriptor;
            }
        }
        public void Parent(PackInput parent) => Manifest["base"] = new JsonObject
        {
            ["id"] = parent.Manifest["id"]!.DeepClone(),
            ["revision"] = parent.Manifest["revision"]!.DeepClone(),
            ["sha256"] = ContentIdentity.Sha256(Encoding.UTF8.GetBytes(parent.Manifest.ToJsonString()))
        };
        public MountedPack Mount() => new(new(Manifest["id"]!.GetValue<string>() + "/content.json", new(Encoding.UTF8.GetBytes(Manifest.ToJsonString()))), (scope, path) =>
        {
            Scopes.Add(scope); string full = scope.Length == 0 ? path : scope + "/" + path; Reads.Add(full);
            return new(Manifest["id"]!.GetValue<string>() + "/" + full, new(Files[full]));
        });
    }
    private static string Marker(ResolvedDocument document) => document.Json.GetProperty("marker").GetString()!;
    private static void PackOwnedReceipts()
    {
        var input = new PackInput(); var pack = ContentPacks.Resolve(input.Mount(), requestedLocale: "missing-language", requireClassic: true);
        Equal(1, pack.Manifests.Count); Equal("authored.root", pack.Manifests[0].Pack.Id); Equal("1", pack.Manifests[0].Pack.Revision);
        Equal("MIT", pack.Manifests[0].Provenance.License); Equal(4, pack.Components.Count); Equal(5, pack.Localized.Count);
        Equal("en", pack.SelectedLocale); Equal(GameplayContent.ClassicSha256, pack.Gameplay.Bundle.SemanticSha256);
        Equal(true, pack.Gameplay.Manifest is null && pack.Compatibility is null); Equal(9, input.Reads.Count);
        Equal(new OwnedList<string>(["cityNames.json", "cityPreferences.json", "events.json", "rules.json", "council.en.json", "documents.en.json", "encyclopedia.en.json", "menus.en.json", "text.en.json"]), new(input.Reads));
        input.Files.Clear(); Equal("root", Marker(pack.Components["rules"])); Equal(true, pack.Components["rules"].File.Bytes.Count != 0);
    }
    private static void PackOverlayOrder()
    {
        var root = new PackInput();
        root.Manifest["compatibility"] = root.Source("compatibility.json", "{\"format\":\"openciv2-classic-compatibility/1\",\"profile\":\"classic-mge-5.4.0f\"}");
        root.Manifest["resources"] = new JsonArray(new JsonObject { ["id"] = "sound.parent", ["kind"] = "audio", ["path"] = "old.wav", ["sha256"] = new string('0', 64) });
        root.Manifest["scenarios"] = new JsonArray(new JsonObject { ["id"] = "scenario.parent", ["world"] = null });
        var middle = new PackInput("authored.middle", false); middle.Parent(root); middle.Document("rules", "", "rules.json", "middle-only");
        foreach (string kind in ContentPacks.LocalizedComponents) middle.Document(kind, "fr", kind + ".fr.json", "fr-middle");
        middle.Manifest["compatibility"] = null; middle.Manifest["resources"] = new JsonArray(); middle.Manifest["scenarios"] = new JsonArray();
        var leaf = new PackInput("authored.leaf", false); leaf.Parent(middle); leaf.Manifest["defaultLocale"] = "fr"; leaf.Document("text", "fr", "text.fr.json", "leaf-only");
        var pack = ContentPacks.Resolve(leaf.Mount(), [root.Mount(), middle.Mount()], "fr");
        Equal(3, pack.Manifests.Count); Equal("authored.root", pack.Manifests[0].Pack.Id); Equal("authored.leaf", pack.Manifests[2].Pack.Id);
        Equal("authored.middle", pack.Components["rules"].File.Owner.Id); Equal("middle-only", Marker(pack.Components["rules"]));
        Equal("authored.root", pack.Components["cityNames"].File.Owner.Id); Equal("authored.leaf", pack.Localized["text"]["fr"].File.Owner.Id);
        Equal("authored.middle", pack.Localized["council"]["fr"].File.Owner.Id); Equal("authored.root", pack.Localized["text"]["en"].File.Owner.Id);
        Equal("fr", pack.SelectedLocale); Equal(0, pack.Resources.Count); Equal(0, pack.Scenarios.Count); Equal(true, pack.Compatibility is null);
        foreach (string unused in new[] { "rules.json", "old.wav", "compatibility.json" }) Equal(false, root.Reads.Contains(unused));
        Equal(false, middle.Reads.Contains("text.fr.json"));
        var again = ContentPacks.Resolve(leaf.Mount(), [middle.Mount(), root.Mount()], "not-advertised");
        Equal("fr", again.SelectedLocale); Equal(pack.Components["rules"].File.Receipt.Sha256, again.Components["rules"].File.Receipt.Sha256);
    }
    private static void PackParentAdmission()
    {
        var root = new PackInput(); var leaf = new PackInput("authored.leaf", false); leaf.Parent(root);
        ContentError("missing-parent", () => ContentPacks.Resolve(leaf.Mount())); Equal(0, leaf.Reads.Count);
        var unrelated = new MountedPack(new("unused", new("not JSON"u8.ToArray())), (_, _) => throw new InvalidOperationException("Unused provider"));
        MountedPack[] parents = [unrelated, root.Mount()];
        Equal(2, ContentPacks.Resolve(leaf.Mount(), parents).Manifests.Count);
        ContentError("ambiguous-parent", () => ContentPacks.Resolve(leaf.Mount(), [root.Mount(), root.Mount()]));
        leaf.Manifest["base"]!["id"] = "authored.different"; ContentError("parent-identity", () => ContentPacks.Resolve(leaf.Mount(), parents));
        leaf.Manifest["base"]!["id"] = "authored.leaf"; ContentError("dependency-cycle", () => ContentPacks.Resolve(leaf.Mount(), parents));
        root.Manifest["base"] = new JsonObject { ["id"] = "authored.leaf", ["revision"] = 1, ["sha256"] = new string('0', 64) }; leaf.Parent(root);
        ContentError("dependency-cycle", () => ContentPacks.Resolve(leaf.Mount(), [root.Mount()]));
        leaf.Manifest["base"]!["sha256"] = ContentIdentity.Sha256(unrelated.Manifest.Bytes.ToArray()); ContentError("invalid-json", () => ContentPacks.Resolve(leaf.Mount(), parents));
    }
    private static void PackDeclarationAdmission()
    {
        Action<PackInput>[] changes = [i => i.Manifest.Remove("gameplay"), i => i.Manifest.Remove("resources"),
            i => ((JsonObject)i.Manifest["files"]!).Remove("rules"), i => i.Manifest["files"]!["text"] = new JsonObject(),
            i => i.Manifest["profile"] = "invented", i => i.Manifest["files"]!["unknown"] = i.Manifest["files"]!["rules"]!.DeepClone(),
            i => i.Manifest["extensions"] = new JsonObject { ["single"] = 1 }, i => i.Manifest["gameplay"]!["revision"] = 2,
            i => i.Manifest["gameplay"]!["extra"] = 1, i => i.Manifest["files"]!["rules"]!["sha256"] = "ABC",
            i => i.Manifest["scenarios"] = new JsonArray(new JsonObject { ["id"] = "scenario.one", ["world"] = null }, new JsonObject { ["id"] = "scenario.one", ["world"] = null }),
            i => { var r = i.Source("one.wav", "wave"); r["id"] = "sound.one"; r["kind"] = "audio"; i.Manifest["resources"] = new JsonArray(r, r.DeepClone()); },
            i => { var r = i.Source("one.wav", "wave"); r["id"] = "sound.one"; r["kind"] = "audio"; var r2 = r.DeepClone(); r2["id"] = "sound.two"; i.Manifest["resources"] = new JsonArray(r, r2); }];
        foreach (var change in changes) { var input = new PackInput(); change(input); ContentError("invalid-content", () => ContentPacks.Resolve(input.Mount())); Equal(0, input.Reads.Count); }
        var bad = new PackInput(); bad.Manifest["files"]!["rules"]!["path"] = "../outside.json";
        ContentError("invalid-path", () => ContentPacks.Resolve(bad.Mount())); Equal(0, bad.Reads.Count);
    }
    private static void PackLocaleAdmission()
    {
        var root = new PackInput(); var leaf = new PackInput("authored.leaf", false); leaf.Parent(root); leaf.Document("text", "fr", "text.fr.json", "fragment");
        ContentError("incomplete-locale", () => ContentPacks.Resolve(leaf.Mount(), [root.Mount()], "fr")); Equal(0, root.Reads.Count); Equal(0, leaf.Reads.Count);
        leaf = new("authored.leaf", false); leaf.Parent(root); leaf.Manifest["defaultLocale"] = "absent";
        ContentError("incomplete-locale", () => ContentPacks.Resolve(leaf.Mount(), [root.Mount()]));
        foreach (string value in new[] { "{\"format\":\"openciv2-text/1\",\"locale\":\"fr\"}", "{\"format\":\"openciv2-text/2\",\"locale\":\"en\"}" })
        { root.Manifest["files"]!["text"]!["en"] = root.Source("text.en.json", value); ContentError("invalid-content", () => ContentPacks.Resolve(root.Mount())); }
        root = new();
        foreach (string locale in new[] { "😀", "\ue000" }) foreach (string kind in ContentPacks.LocalizedComponents) root.Document(kind, locale, kind + "." + locale + ".json", "unicode locale");
        var pack = ContentPacks.Resolve(root.Mount());
        Equal(new OwnedList<string>(["en", "\ue000", "😀"]), new(pack.Localized["text"].Keys));
    }
    private static void PackOwnedMediaAndHashes()
    {
        var root = new PackInput(); root.Files["rules.json"] = "not JSON"u8.ToArray(); ContentError("hash-mismatch", () => ContentPacks.Resolve(root.Mount()));
        root.Manifest["files"]!["rules"] = root.Source("rules.json", "not JSON"); ContentError("invalid-json", () => ContentPacks.Resolve(root.Mount()));
        root = new();
        root.Manifest["scenarios"] = new JsonArray(new JsonObject { ["id"] = "scenario.one", ["world"] = root.Source("one.scn", [0, 1, 255]) }, new JsonObject { ["id"] = "scenario.external", ["world"] = null });
        var resource = root.Source("one.wav", "raw audio input"); resource["id"] = "sound.one"; resource["kind"] = "audio"; root.Manifest["resources"] = new JsonArray(resource);
        root.Manifest["compatibility"] = root.Source("compatibility.json", "{\"format\":\"openciv2-classic-compatibility/1\",\"profile\":\"classic-mge-5.4.0f\"}");
        var pack = ContentPacks.Resolve(root.Mount()); Equal(2, pack.Scenarios.Count); BytesEqual([0, 1, 255], pack.Scenarios[0].World!.Bytes);
        Equal(true, pack.Scenarios[1].World is null); BytesEqual("raw audio input"u8.ToArray(), pack.Resources[0].File.Bytes);
        Equal(true, pack.Resources[0].Kind == ResourceKind.Audio && pack.Compatibility is not null);
        root.Files.Remove("one.wav"); ContentError("source-error", () => ContentPacks.Resolve(root.Mount()));
    }
    private static void PackNestedGameplay()
    {
        var root = new PackInput(); var bundle = new BundleInput(); bundle.Custom(); bundle.Edit("combat", d => d["antiAirMultipliers"]!["ordinaryAircraft"] = 7);
        foreach (var (path, bytes) in bundle.Files) root.Files["variant/" + path] = bytes;
        root.Manifest["gameplay"] = root.Source("variant/gameplay.json", bundle.Manifest.ToJsonString());
        var leaf = new PackInput("authored.leaf", false); leaf.Parent(root);
        var pack = ContentPacks.Resolve(leaf.Mount(), [root.Mount()]);
        Equal("authored.root", pack.Gameplay.Owner.Id); Equal("variant/gameplay.json", pack.Gameplay.Manifest!.Receipt.Path);
        Equal(7, pack.Gameplay.Bundle.Values.Combat.OrdinaryAircraftMultiplier); Equal(false, pack.Gameplay.Bundle.ClassicValues);
        Equal(6, root.Scopes.Count(s => s == "variant")); Equal(0, leaf.Reads.Count);
        ContentError("classic-values-required", () => ContentPacks.Resolve(leaf.Mount(), [root.Mount()], requireClassic: true));
        leaf.Manifest["gameplay"] = new JsonObject { ["builtin"] = GameplayContent.Target, ["revision"] = 1 }; root.Reads.Clear();
        Equal(true, ContentPacks.Resolve(leaf.Mount(), [root.Mount()], requireClassic: true).Gameplay.Bundle.ClassicValues);
        Equal(false, root.Reads.Contains("variant/gameplay.json"));
    }
    private static void PackBudgetsAndRevisions()
    {
        var root = new PackInput(); var leaf = new PackInput("authored.leaf", false);
        root.Manifest["revision"] = ContentJson.Parse("9007199254740993"u8, "revision"); leaf.Parent(root);
        Equal("9007199254740993", ContentPacks.Resolve(leaf.Mount(), [root.Mount()]).Manifests[0].Pack.Revision);
        ContentError("resource-limit", () => ContentPacks.Resolve(leaf.Mount(), [root.Mount()], limits: new(MaximumChain: 1)));
        ContentError("resource-limit", () => ContentPacks.Resolve(root.Mount(), limits: new(MaximumFiles: 8)));
        root.Reads.Clear(); ContentError("resource-limit", () => ContentPacks.Resolve(root.Mount(), limits: new(MaximumBytes: root.Mount().Manifest.Bytes.Count - 1))); Equal(0, root.Reads.Count);
        var descriptor = root.Manifest["files"]!["rules"]!.DeepClone(); root.Manifest["scenarios"] = new JsonArray(new JsonObject { ["id"] = "scenario.alias", ["world"] = descriptor });
        root.Reads.Clear(); var cached = ContentPacks.Resolve(root.Mount(), limits: new(MaximumFiles: 9));
        Equal(9, root.Reads.Count); Equal(true, ReferenceEquals(cached.Scenarios[0].World, cached.Components["rules"].File));
    }
}
