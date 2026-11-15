module openciv.formats.content_pack;

import std;
import openciv.formats.content_reader;
import openciv.base.hash;

// Native data-format.md sections 2, 3 and 9; gameplay-format.md section 1.

namespace openciv::formats::content {
namespace {
using detail::Reader;
struct Descriptor {
    std::string path, sha256;
};
struct Scenario {
    std::string id;
    std::optional<Descriptor> world;
};
struct Resource {
    std::string id;
    ResourceKind kind;
    Descriptor file;
};
struct Gameplay {
    bool builtin = false;
    Descriptor file;
};
struct Manifest {
    const MountedPack* mount = nullptr;
    PackReceipt receipt;
    std::optional<PackIdentity> base;
    std::string default_locale;
    std::map<std::string, Descriptor> components;
    std::map<std::string, std::map<std::string, Descriptor>> localized;
    std::optional<std::optional<Descriptor>> compatibility;
    std::optional<std::vector<Scenario>> scenarios;
    std::optional<std::vector<Resource>> resources;
    std::optional<Gameplay> gameplay;
};
Descriptor descriptor(Reader reader, bool resource = false) {
    if (!resource)
        reader.fields({"path", "sha256"});
    Descriptor out{reader.at("path").text(), reader.at("sha256").sha256()};
    validate_relative_path(out.path);
    return out;
}
std::vector<std::string> texts(Reader reader, bool nonempty) {
    std::vector<std::string> out;
    for (std::size_t i = 0; i < reader.array_size(); ++i)
        out.push_back(reader.element(i).text(nonempty));
    return out;
}
Manifest parse_manifest(const MountedPack& mount) {
    if (mount.manifest.identity.empty())
        throw Error("source-error", "content.json", "missing mounted manifest identity");
    auto parsed = parse_json(
        {reinterpret_cast<const char*>(mount.manifest.bytes.data()), mount.manifest.bytes.size()},
        mount.manifest.identity);
    Reader root{parsed.value, parsed.numbers, mount.manifest.identity};
    root.members(
        {"format", "id", "revision", "profile", "defaultLocale", "base", "files", "provenance"},
        {"compatibility", "scenarios", "resources", "gameplay"}, true);
    root.at("format").constant("openciv2-content-pack/1");
    root.at("profile").constant(gameplay_target);
    Manifest out;
    out.mount = &mount;
    out.receipt.pack = {root.at("id").id(), root.at("revision").revision(),
                        base::sha256(mount.manifest.bytes)};
    out.receipt.source_identity = mount.manifest.identity;
    out.default_locale = root.at("defaultLocale").text();
    auto provenance = root.at("provenance");
    provenance.fields({"authors", "license", "notes"});
    out.receipt.provenance = {texts(provenance.at("authors"), true),
                              provenance.at("license").text(),
                              texts(provenance.at("notes"), false)};
    auto parent = root.at("base");
    if (!parent.value.isNull()) {
        parent.fields({"id", "revision", "sha256"});
        out.base = PackIdentity{parent.at("id").id(), parent.at("revision").revision(),
                                parent.at("sha256").sha256()};
    } else {
        for (const auto key : {"compatibility", "scenarios", "resources", "gameplay"})
            if (!root.has(key))
                root.error("root pack requires " + std::string(key));
    }
    auto files = root.at("files");
    constexpr std::array<std::string_view, 9> keys{"rules",  "cityNames", "cityPreferences",
                                                   "events", "text",      "encyclopedia",
                                                   "menus",  "documents", "council"};
    if (out.base)
        files.members({}, keys);
    else
        files.fields(keys);
    for (const auto key : plain_components)
        if (files.has(key))
            out.components.emplace(key, descriptor(files.at(key)));
    for (const auto key : localized_components) {
        if (!files.has(key))
            continue;
        auto locales = files.at(key);
        if (!out.base && locales.object().empty())
            locales.error("root localized catalog must not be empty");
        for (const auto& [locale, child] : locales.object()) {
            (void)child;
            if (locale.empty())
                locales.error("empty locale tag");
            out.localized[std::string(key)].emplace(locale, descriptor(locales.at(locale)));
        }
    }
    if (root.has("compatibility")) {
        out.compatibility.emplace();
        if (!root.at("compatibility").value.isNull())
            *out.compatibility = descriptor(root.at("compatibility"));
    }
    if (root.has("gameplay")) {
        auto selection = root.at("gameplay");
        if (selection.has("builtin")) {
            selection.fields({"builtin", "revision"});
            selection.at("builtin").constant(gameplay_target);
            if (selection.at("revision").revision() != "1")
                selection.error("unknown builtin revision");
            out.gameplay = Gameplay{true, {}};
        } else
            out.gameplay = Gameplay{false, descriptor(selection)};
    }
    if (root.has("scenarios")) {
        out.scenarios.emplace();
        std::set<std::string> ids;
        auto scenarios = root.at("scenarios");
        for (std::size_t i = 0; i < scenarios.array_size(); ++i) {
            auto row = scenarios.element(i);
            row.fields({"id", "world"});
            Scenario scenario{row.at("id").id(), {}};
            if (!ids.insert(scenario.id).second)
                row.error("duplicate scenario identity");
            if (!row.at("world").value.isNull())
                scenario.world = descriptor(row.at("world"));
            out.scenarios->push_back(std::move(scenario));
        }
    }
    if (root.has("resources")) {
        out.resources.emplace();
        std::set<std::string> ids, paths;
        auto resources = root.at("resources");
        for (std::size_t i = 0; i < resources.array_size(); ++i) {
            auto row = resources.element(i);
            row.fields({"id", "kind", "path", "sha256"});
            Resource resource{row.at("id").id(), ResourceKind::audio, descriptor(row, true)};
            const auto kind = row.at("kind").text();
            if (kind == "image")
                resource.kind = ResourceKind::image;
            else if (kind == "video")
                resource.kind = ResourceKind::video;
            else if (kind != "audio")
                row.at("kind").error("unknown loose resource kind");
            if (!ids.insert(resource.id).second)
                row.error("duplicate resource identity");
            if (!paths.insert(resource.file.path).second)
                row.error("duplicate resource path");
            out.resources->push_back(std::move(resource));
        }
    }
    return out;
}
struct BoundDescriptor {
    const Manifest* owner;
    Descriptor file;
};
class Loader {
public:
    explicit Loader(ResolutionLimits limits) : limits_(limits) {}
    void account(std::size_t size, std::string_view path) {
        if (size > limits_.maximum_bytes - bytes_)
            throw Error("resource-limit", std::string(path),
                        "selected content exceeds byte budget");
        bytes_ += size;
    }
    std::shared_ptr<const ResolvedFile> raw(const Manifest& owner, std::string_view directory,
                                            std::string_view path) {
        validate_relative_path(path);
        if (!directory.empty())
            validate_relative_path(directory);
        const auto location = directory.empty() ? std::string(path)
                                                : std::string(directory) + "/" + std::string(path);
        const auto key =
            std::make_tuple(owner.receipt.pack.sha256, std::string(directory), std::string(path));
        if (const auto found = files_.find(key); found != files_.end())
            return found->second;
        if (files_.size() >= limits_.maximum_files)
            throw Error("resource-limit", location, "too many selected content files");
        Source source;
        try {
            source = owner.mount->files(directory, path);
        } catch (const std::bad_alloc&) {
            throw;
        } catch (const std::exception& error) {
            throw Error("source-error", owner.receipt.pack.id + "/" + location, error.what());
        }
        if (source.identity.empty())
            throw Error("source-error", location, "missing mounted file identity");
        account(source.bytes.size(), location);
        auto file = std::make_shared<ResolvedFile>();
        file->owner = owner.receipt.pack;
        file->receipt = {location, std::move(source.identity), base::sha256(source.bytes)};
        file->bytes = std::move(source.bytes);
        files_.emplace(key, file);
        return file;
    }
    std::shared_ptr<const ResolvedFile> read(const BoundDescriptor& descriptor) {
        auto file = raw(*descriptor.owner, {}, descriptor.file.path);
        if (file->receipt.sha256 != descriptor.file.sha256)
            throw Error("hash-mismatch", file->owner.id + "/" + file->receipt.path,
                        "exact file hash mismatch");
        return file;
    }
    ResolvedDocument document(const BoundDescriptor& descriptor, std::string_view format,
                              std::string_view locale = {}) {
        auto file = read(descriptor);
        const auto location = file->owner.id + "/" + file->receipt.path;
        auto json = parse_json(
            {reinterpret_cast<const char*>(file->bytes.data()), file->bytes.size()}, location);
        Reader root{json.value, json.numbers, location};
        root.at("format").constant(format);
        if (!locale.empty())
            root.at("locale").constant(locale);
        return {std::move(file), std::move(json)};
    }

private:
    ResolutionLimits limits_;
    std::size_t bytes_ = 0;
    std::map<std::tuple<std::string, std::string, std::string>, std::shared_ptr<const ResolvedFile>>
        files_;
};
std::string component_format(std::string_view name) {
    if (name == "cityNames")
        return "openciv2-city-names/1";
    if (name == "cityPreferences")
        return "openciv2-city-preferences/1";
    return "openciv2-" + std::string(name) + "/1";
}
} // namespace
std::shared_ptr<const ResolvedPack> resolve_pack(const MountedPack& selected,
                                                 std::span<const MountedPack> parents,
                                                 std::string_view requested_locale,
                                                 bool require_classic, ResolutionLimits limits) {
    Loader loader(limits);
    // Hash registered bytes without decoding unrelated/unchecked parent input.
    std::map<std::string, std::vector<const MountedPack*>> catalog;
    for (const auto& parent : parents)
        catalog[base::sha256(parent.manifest.bytes)].push_back(&parent);
    std::vector<Manifest> chain;
    std::set<std::pair<std::string, std::string>> visited;
    const MountedPack* next = &selected;
    std::optional<PackIdentity> expected;
    while (next) {
        if (chain.size() >= limits.maximum_chain)
            throw Error("resource-limit", selected.manifest.identity,
                        "pack inheritance chain exceeds limit");
        loader.account(next->manifest.bytes.size(), next->manifest.identity);
        auto current = parse_manifest(*next);
        const auto& identity = current.receipt.pack;
        if (expected && identity != *expected)
            throw Error("parent-identity", next->manifest.identity,
                        "parent ID/revision does not match its pinned manifest");
        if (!visited.emplace(identity.id, identity.revision).second)
            throw Error("dependency-cycle", next->manifest.identity,
                        "repeated pack ID/revision in inheritance chain");
        expected = current.base;
        chain.push_back(std::move(current));
        next = nullptr;
        if (expected) {
            if (visited.contains({expected->id, expected->revision}))
                throw Error("dependency-cycle", chain.back().receipt.pack.id,
                            "parent repeats an active pack ID/revision");
            const auto found = catalog.find(expected->sha256);
            if (found == catalog.end())
                throw Error("missing-parent", expected->id,
                            "exact parent manifest is not explicitly mounted");
            if (found->second.size() != 1)
                throw Error("ambiguous-parent", expected->id,
                            "parent hash has multiple mounted sources");
            next = found->second.front();
        }
    }
    std::reverse(chain.begin(), chain.end());
    auto out = std::make_shared<ResolvedPack>();
    out->profile = gameplay_target;
    out->default_locale = chain.back().default_locale;
    std::map<std::string, BoundDescriptor> components;
    std::map<std::string, std::map<std::string, BoundDescriptor>> localized;
    std::optional<BoundDescriptor> compatibility;
    const Manifest *scenario_owner = nullptr, *resource_owner = nullptr, *gameplay_owner = nullptr;
    for (const auto& manifest : chain) {
        out->manifests.push_back(manifest.receipt);
        for (const auto& [key, file] : manifest.components)
            components.insert_or_assign(key, BoundDescriptor{&manifest, file});
        for (const auto& [key, locales] : manifest.localized)
            for (const auto& [locale, file] : locales)
                localized[key].insert_or_assign(locale, BoundDescriptor{&manifest, file});
        if (manifest.compatibility) {
            compatibility.reset();
            if (*manifest.compatibility)
                compatibility = BoundDescriptor{&manifest, **manifest.compatibility};
        }
        if (manifest.scenarios)
            scenario_owner = &manifest;
        if (manifest.resources)
            resource_owner = &manifest;
        if (manifest.gameplay)
            gameplay_owner = &manifest;
    }
    std::set<std::string> advertised;
    for (const auto& [key, locales] : localized) {
        (void)key;
        for (const auto& [locale, file] : locales) {
            (void)file;
            advertised.insert(locale);
        }
    }
    if (!advertised.contains(out->default_locale))
        throw Error("incomplete-locale", out->default_locale, "default locale is absent");
    for (const auto& locale : advertised)
        for (const auto key : localized_components)
            if (!localized[std::string(key)].contains(locale))
                throw Error("incomplete-locale", locale,
                            "missing localized component " + std::string(key));
    out->selected_locale = advertised.contains(std::string(requested_locale))
                               ? std::string(requested_locale)
                               : out->default_locale;
    for (const auto& [key, file] : components)
        out->components.emplace(key, loader.document(file, component_format(key)));
    for (const auto& [key, locales] : localized)
        for (const auto& [locale, file] : locales)
            out->localized[key].emplace(locale,
                                        loader.document(file, component_format(key), locale));
    if (compatibility) {
        out->compatibility = loader.document(*compatibility, "openciv2-classic-compatibility/1");
        auto& json = out->compatibility->json;
        Reader{json.value, json.numbers, compatibility->file.path}.at("profile").constant(
            gameplay_target);
    }
    for (const auto& scenario : *scenario_owner->scenarios)
        out->scenarios.push_back({scenario.id, scenario.world
                                                   ? loader.read({scenario_owner, *scenario.world})
                                                   : nullptr});
    for (const auto& resource : *resource_owner->resources)
        out->resources.push_back(
            {resource.id, resource.kind, loader.read({resource_owner, resource.file})});
    out->gameplay.owner = gameplay_owner->receipt.pack;
    if (gameplay_owner->gameplay->builtin)
        out->gameplay.bundle = builtin_gameplay();
    else {
        const auto& selection = gameplay_owner->gameplay->file;
        out->gameplay.manifest = loader.read({gameplay_owner, selection});
        const auto slash = selection.path.rfind('/');
        const auto directory =
            slash == std::string::npos ? std::string{} : selection.path.substr(0, slash);
        const auto resolve = [&](std::string_view path) {
            const auto file = loader.raw(*gameplay_owner, directory, path);
            return Source{file->receipt.identity, file->bytes};
        };
        out->gameplay.bundle =
            load_gameplay(out->gameplay.manifest->bytes, resolve, require_classic);
    }
    return out;
}
} // namespace openciv::formats::content
