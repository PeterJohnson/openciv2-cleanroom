using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenCiv2.Base;

namespace OpenCiv2.Formats.Content;

public sealed record PackIdentity(string Id, string Revision, string Sha256);
public sealed record Provenance(OwnedList<string> Authors, string License, OwnedList<string> Notes);
public sealed record PackReceipt(PackIdentity Pack, string SourceIdentity, Provenance Provenance);
public sealed record MountedPack(ContentSource Manifest, Func<string, string, ContentSource> Files);
public sealed record ResolvedFile(PackIdentity Owner, FileReceipt Receipt, OwnedList<byte> Bytes);
public sealed class ResolvedDocument
{
    public ResolvedFile File { get; }
    public JsonElement Json { get; }
    internal ResolvedDocument(ResolvedFile file, JsonNode node)
    {
        File = file;
        using var document = JsonDocument.Parse(node.ToJsonString()); Json = document.RootElement.Clone();
    }
    internal JsonNode Read() => JsonNode.Parse(Json.GetRawText())!;
}
public enum ResourceKind { Audio, Image, Video }
public sealed record ResolvedResource(string Id, ResourceKind Kind, ResolvedFile File);
public sealed record ResolvedScenario(string Id, ResolvedFile? World);
public sealed record ResolvedGameplay(PackIdentity Owner, ResolvedFile? Manifest, GameplayBundle Bundle);
public sealed record ResolvedPack(OwnedList<PackReceipt> Manifests, string Profile, string DefaultLocale, string SelectedLocale,
    ImmutableSortedDictionary<string, ResolvedDocument> Components,
    ImmutableSortedDictionary<string, ImmutableSortedDictionary<string, ResolvedDocument>> Localized,
    ResolvedDocument? Compatibility, OwnedList<ResolvedScenario> Scenarios, OwnedList<ResolvedResource> Resources, ResolvedGameplay Gameplay);
public sealed record ResolutionLimits(int MaximumChain = 64, int MaximumFiles = 4096, long MaximumBytes = 256 * 1024 * 1024);

internal sealed class Utf8Order : IComparer<string>
{
    public static Utf8Order Instance { get; } = new();
    public int Compare(string? left, string? right)
    {
        if (left is null || right is null) return left is null ? right is null ? 0 : -1 : 1;
        var a = left.EnumerateRunes(); var b = right.EnumerateRunes();
        while (a.MoveNext())
        {
            if (!b.MoveNext()) return 1;
            int order = a.Current.Value.CompareTo(b.Current.Value); if (order != 0) return order;
        }
        return b.MoveNext() ? -1 : 0;
    }
}

public static class ContentPacks
{
    public const string ResolverVersion = "openciv2-content-resolution/1";
    public static OwnedList<string> PlainComponents { get; } = new(["rules", "cityNames", "cityPreferences", "events"]);
    public static OwnedList<string> LocalizedComponents { get; } = new(["text", "encyclopedia", "menus", "documents", "council"]);
    private sealed record Descriptor(string Path, string Sha256);
    private sealed record Scenario(string Id, Descriptor? World);
    private sealed record Resource(string Id, ResourceKind Kind, Descriptor File);
    private sealed record Gameplay(bool Builtin, Descriptor? File);
    private sealed class Manifest(MountedPack mount, PackReceipt receipt, PackIdentity? parent, string defaultLocale)
    {
        public MountedPack Mount { get; } = mount;
        public PackReceipt Receipt { get; } = receipt;
        public PackIdentity? Parent { get; } = parent;
        public string DefaultLocale { get; } = defaultLocale;
        public SortedDictionary<string, Descriptor> Components { get; } = new(Utf8Order.Instance);
        public SortedDictionary<string, SortedDictionary<string, Descriptor>> Localized { get; } = new(Utf8Order.Instance);
        public bool HasCompatibility { get; set; }
        public Descriptor? Compatibility { get; set; }
        public List<Scenario>? Scenarios { get; set; }
        public List<Resource>? Resources { get; set; }
        public Gameplay? Gameplay { get; set; }
    }
    private static Descriptor Describe(ContentReader reader, bool resource = false)
    {
        if (!resource) reader.Fields(["path", "sha256"]);
        string path = reader.At("path").Text(), sha = reader.At("sha256").Sha256(); ContentPaths.ValidateRelative(path);
        return new(path, sha);
    }
    private static OwnedList<string> Texts(ContentReader reader, bool nonempty) => new(Enumerable.Range(0, reader.ArraySize()).Select(i => reader.Element(i).Text(nonempty)));
    private static Manifest Parse(MountedPack mount)
    {
        if (mount.Manifest.Identity.Length == 0) throw new ContentException("source-error", "content.json", "missing mounted manifest identity");
        var root = new ContentReader(ContentJson.Parse(mount.Manifest.Bytes.ToArray(), mount.Manifest.Identity), mount.Manifest.Identity);
        root.Members(["format", "id", "revision", "profile", "defaultLocale", "base", "files", "provenance"], ["compatibility", "scenarios", "resources", "gameplay"], true);
        root.At("format").Constant("openciv2-content-pack/1"); root.At("profile").Constant(GameplayContent.Target);
        var identity = new PackIdentity(root.At("id").Id(), root.At("revision").Revision(), ContentIdentity.Sha256(mount.Manifest.Bytes.ToArray()));
        string defaultLocale = root.At("defaultLocale").Text();
        var provenance = root.At("provenance"); provenance.Fields(["authors", "license", "notes"]);
        var receipt = new PackReceipt(identity, mount.Manifest.Identity, new(Texts(provenance.At("authors"), true), provenance.At("license").Text(), Texts(provenance.At("notes"), false)));
        var parent = root.At("base"); PackIdentity? baseIdentity = null;
        if (parent.Value is not null)
        {
            parent.Fields(["id", "revision", "sha256"]); baseIdentity = new(parent.At("id").Id(), parent.At("revision").Revision(), parent.At("sha256").Sha256());
        }
        else foreach (string key in new[] { "compatibility", "scenarios", "resources", "gameplay" }) if (!root.Has(key)) root.Error("root pack requires " + key);
        var result = new Manifest(mount, receipt, baseIdentity, defaultLocale);
        var files = root.At("files"); string[] keys = [.. PlainComponents, .. LocalizedComponents];
        if (baseIdentity is null) files.Fields(keys); else files.Members([], keys);
        foreach (string key in PlainComponents) if (files.Has(key)) result.Components.Add(key, Describe(files.At(key)));
        foreach (string key in LocalizedComponents)
        {
            if (!files.Has(key)) continue;
            var locales = files.At(key); var obj = locales.Object();
            if (baseIdentity is null && obj.Count == 0) locales.Error("root localized catalog must not be empty");
            foreach (string locale in obj.Select(p => p.Key).Order(Utf8Order.Instance))
            {
                if (locale.Length == 0) locales.Error("empty locale tag");
                if (!result.Localized.TryGetValue(key, out var catalog)) result.Localized.Add(key, catalog = new(Utf8Order.Instance));
                catalog.Add(locale, Describe(locales.At(locale)));
            }
        }
        result.HasCompatibility = root.Has("compatibility");
        if (result.HasCompatibility && root.At("compatibility").Value is not null) result.Compatibility = Describe(root.At("compatibility"));
        if (root.Has("gameplay"))
        {
            var selection = root.At("gameplay");
            if (selection.Has("builtin"))
            {
                selection.Fields(["builtin", "revision"]); selection.At("builtin").Constant(GameplayContent.Target);
                if (selection.At("revision").Revision() != "1") selection.Error("unknown builtin revision");
                result.Gameplay = new(true, null);
            }
            else result.Gameplay = new(false, Describe(selection));
        }
        if (root.Has("scenarios"))
        {
            result.Scenarios = []; var ids = new HashSet<string>(StringComparer.Ordinal); var scenarios = root.At("scenarios");
            for (int i = 0; i < scenarios.ArraySize(); ++i)
            {
                var row = scenarios.Element(i); row.Fields(["id", "world"]); string id = row.At("id").Id();
                if (!ids.Add(id)) row.Error("duplicate scenario identity");
                result.Scenarios.Add(new(id, row.At("world").Value is null ? null : Describe(row.At("world"))));
            }
        }
        if (root.Has("resources"))
        {
            result.Resources = []; var ids = new HashSet<string>(StringComparer.Ordinal); var paths = new HashSet<string>(StringComparer.Ordinal); var resources = root.At("resources");
            for (int i = 0; i < resources.ArraySize(); ++i)
            {
                var row = resources.Element(i); row.Fields(["id", "kind", "path", "sha256"]);
                string id = row.At("id").Id(); var file = Describe(row, true); string kind = row.At("kind").Text();
                if (kind is not ("audio" or "image" or "video")) row.At("kind").Error("unknown loose resource kind");
                if (!ids.Add(id)) row.Error("duplicate resource identity"); if (!paths.Add(file.Path)) row.Error("duplicate resource path");
                result.Resources.Add(new(id, kind == "audio" ? ResourceKind.Audio : kind == "image" ? ResourceKind.Image : ResourceKind.Video, file));
            }
        }
        return result;
    }
    private sealed record Bound(Manifest Owner, Descriptor File);
    private sealed class Loader(ResolutionLimits limits)
    {
        private long bytes;
        private readonly Dictionary<(string Manifest, string Directory, string Path), ResolvedFile> files = [];
        public void Account(int size, string path)
        {
            if (size > limits.MaximumBytes - bytes) throw new ContentException("resource-limit", path, "selected content exceeds byte budget");
            bytes += size;
        }
        public ResolvedFile Raw(Manifest owner, string directory, string path)
        {
            ContentPaths.ValidateRelative(path); if (directory.Length != 0) ContentPaths.ValidateRelative(directory);
            string location = directory.Length == 0 ? path : directory + "/" + path;
            var key = (owner.Receipt.Pack.Sha256, directory, path);
            if (files.TryGetValue(key, out var cached)) return cached;
            if (files.Count >= limits.MaximumFiles) throw new ContentException("resource-limit", location, "too many selected content files");
            ContentSource source;
            try { source = owner.Mount.Files(directory, path); }
            catch (Exception error) when (error is not OutOfMemoryException) { throw new ContentException("source-error", owner.Receipt.Pack.Id + "/" + location, error.Message); }
            if (source.Identity.Length == 0) throw new ContentException("source-error", location, "missing mounted file identity");
            Account(source.Bytes.Count, location);
            var file = new ResolvedFile(owner.Receipt.Pack, new(location, source.Identity, ContentIdentity.Sha256(source.Bytes.ToArray())), source.Bytes);
            files.Add(key, file); return file;
        }
        public ResolvedFile Read(Bound descriptor)
        {
            var file = Raw(descriptor.Owner, "", descriptor.File.Path);
            if (file.Receipt.Sha256 != descriptor.File.Sha256) throw new ContentException("hash-mismatch", file.Owner.Id + "/" + file.Receipt.Path, "exact file hash mismatch");
            return file;
        }
        public ResolvedDocument Document(Bound descriptor, string format, string locale = "")
        {
            var file = Read(descriptor); string location = file.Owner.Id + "/" + file.Receipt.Path;
            var node = ContentJson.Parse(file.Bytes.ToArray(), location); var reader = new ContentReader(node, location);
            reader.At("format").Constant(format); if (locale.Length != 0) reader.At("locale").Constant(locale);
            return new(file, node!);
        }
    }
    public static string ComponentFormat(string name) => name switch
    {
        "cityNames" => "openciv2-city-names/1",
        "cityPreferences" => "openciv2-city-preferences/1",
        _ => "openciv2-" + name + "/1"
    };
    public static ResolvedPack Resolve(MountedPack selected, IReadOnlyCollection<MountedPack>? parents = null,
        string requestedLocale = "", bool requireClassic = false, ResolutionLimits? limits = null)
    {
        limits ??= new();
        if (limits.MaximumBytes < 0 || limits.MaximumChain < 0 || limits.MaximumFiles < 0) throw new ArgumentOutOfRangeException(nameof(limits));
        var loader = new Loader(limits);
        var catalog = (parents ?? []).GroupBy(p => ContentIdentity.Sha256(p.Manifest.Bytes.ToArray())).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var chain = new List<Manifest>(); var visited = new HashSet<(string Id, string Revision)>();
        MountedPack? next = selected; PackIdentity? expected = null;
        while (next is not null)
        {
            if (chain.Count >= limits.MaximumChain) throw new ContentException("resource-limit", selected.Manifest.Identity, "pack inheritance chain exceeds limit");
            loader.Account(next.Manifest.Bytes.Count, next.Manifest.Identity); var current = Parse(next); var id = current.Receipt.Pack;
            if (expected is not null && id != expected) throw new ContentException("parent-identity", next.Manifest.Identity, "parent ID/revision does not match its pinned manifest");
            if (!visited.Add((id.Id, id.Revision))) throw new ContentException("dependency-cycle", next.Manifest.Identity, "repeated pack ID/revision in inheritance chain");
            expected = current.Parent; chain.Add(current); next = null;
            if (expected is not null)
            {
                if (visited.Contains((expected.Id, expected.Revision))) throw new ContentException("dependency-cycle", id.Id, "parent repeats an active pack ID/revision");
                if (!catalog.TryGetValue(expected.Sha256, out var matches)) throw new ContentException("missing-parent", expected.Id, "exact parent manifest is not explicitly mounted");
                if (matches.Length != 1) throw new ContentException("ambiguous-parent", expected.Id, "parent hash has multiple mounted sources");
                next = matches[0];
            }
        }
        chain.Reverse(); string defaultLocale = chain[^1].DefaultLocale;
        var components = new SortedDictionary<string, Bound>(Utf8Order.Instance);
        var localized = new SortedDictionary<string, SortedDictionary<string, Bound>>(Utf8Order.Instance);
        Bound? compatibility = null; Manifest scenarioOwner = null!, resourceOwner = null!, gameplayOwner = null!;
        foreach (var manifest in chain)
        {
            foreach (var (key, file) in manifest.Components) components[key] = new(manifest, file);
            foreach (var (key, locales) in manifest.Localized)
            {
                if (!localized.TryGetValue(key, out var language)) localized.Add(key, language = new(Utf8Order.Instance));
                foreach (var (locale, file) in locales) language[locale] = new(manifest, file);
            }
            if (manifest.HasCompatibility) compatibility = manifest.Compatibility is null ? null : new(manifest, manifest.Compatibility);
            if (manifest.Scenarios is not null) scenarioOwner = manifest;
            if (manifest.Resources is not null) resourceOwner = manifest;
            if (manifest.Gameplay is not null) gameplayOwner = manifest;
        }
        var advertised = new SortedSet<string>(localized.Values.SelectMany(l => l.Keys), Utf8Order.Instance);
        if (!advertised.Contains(defaultLocale)) throw new ContentException("incomplete-locale", defaultLocale, "default locale is absent");
        foreach (string locale in advertised)
            foreach (string key in LocalizedComponents)
                if (!localized.TryGetValue(key, out var languages) || !languages.ContainsKey(locale))
                    throw new ContentException("incomplete-locale", locale, "missing localized component " + key);
        string selectedLocale = advertised.Contains(requestedLocale) ? requestedLocale : defaultLocale;
        var decoded = components.ToImmutableSortedDictionary(p => p.Key, p => loader.Document(p.Value, ComponentFormat(p.Key)), Utf8Order.Instance);
        var translations = localized.ToImmutableSortedDictionary(p => p.Key, p => p.Value.ToImmutableSortedDictionary(
            l => l.Key, l => loader.Document(l.Value, ComponentFormat(p.Key), l.Key), Utf8Order.Instance), Utf8Order.Instance);
        ResolvedDocument? compat = null;
        if (compatibility is not null)
        {
            compat = loader.Document(compatibility, "openciv2-classic-compatibility/1");
            new ContentReader(compat.Read(), compatibility.File.Path).At("profile").Constant(GameplayContent.Target);
        }
        var scenarios = new OwnedList<ResolvedScenario>(scenarioOwner.Scenarios!.Select(s => new ResolvedScenario(s.Id, s.World is null ? null : loader.Read(new(scenarioOwner, s.World)))));
        var resources = new OwnedList<ResolvedResource>(resourceOwner.Resources!.Select(r => new ResolvedResource(r.Id, r.Kind, loader.Read(new(resourceOwner, r.File)))));
        ResolvedFile? gameplayFile = null; GameplayBundle bundle;
        if (gameplayOwner.Gameplay!.Builtin) bundle = GameplayContent.Builtin;
        else
        {
            var selection = gameplayOwner.Gameplay.File!; gameplayFile = loader.Read(new(gameplayOwner, selection));
            int slash = selection.Path.LastIndexOf('/'); string directory = slash < 0 ? "" : selection.Path[..slash];
            bundle = GameplayContent.Load(gameplayFile.Bytes.ToArray(), path =>
            {
                var file = loader.Raw(gameplayOwner, directory, path); return new(file.Receipt.Identity, file.Bytes);
            }, requireClassic);
        }
        return new(new(chain.Select(m => m.Receipt)), GameplayContent.Target, defaultLocale, selectedLocale, decoded, translations,
            compat, scenarios, resources, new(gameplayOwner.Receipt.Pack, gameplayFile, bundle));
    }
}
