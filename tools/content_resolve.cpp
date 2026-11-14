#include "content_command.hpp"
#include "formats/content_pack.hpp"
#include "platform/content_files.hpp"
#include <iostream>

namespace {
namespace content = openciv::formats::content;
content::MountedPack mount(std::string_view filename) {
    const auto path = std::filesystem::absolute(
        std::filesystem::path(std::u8string(filename.begin(), filename.end())));
    auto directory = std::make_shared<openciv::io::ContentDirectory>(path.parent_path());
    const auto leaf = path.filename().generic_u8string();
    auto source = directory->read(std::string(leaf.begin(), leaf.end()));
    return {std::move(source), [directory](std::string_view scope, std::string_view relative) {
                const auto path = scope.empty() ? std::string(relative)
                                                : std::string(scope) + "/" + std::string(relative);
                return directory->read(path, content::ResolutionLimits{}.maximum_bytes, scope);
            }};
}
jt::Json identity(const content::PackIdentity& value) {
    jt::Json out;
    out["id"] = jt::Json(value.id);
    out["revision"] = jt::Json(value.revision);
    out["sha256"] = jt::Json(value.sha256);
    return out;
}
} // namespace
int run_content_resolve(std::span<const std::string> arguments) {
    if (arguments.empty())
        throw std::invalid_argument("Usage: oc2 content resolve CONTENT_MANIFEST [--parent "
                                    "MANIFEST]... [--locale LOCALE] [--require-classic] [--json]");
    std::vector<std::string> parents;
    std::optional<std::string> locale;
    bool as_json = false, require_classic = false;
    for (std::size_t i = 1; i < arguments.size(); ++i) {
        if (arguments[i] == "--json" && !as_json)
            as_json = true;
        else if (arguments[i] == "--require-classic" && !require_classic)
            require_classic = true;
        else if (arguments[i] == "--parent" && i + 1 < arguments.size())
            parents.push_back(arguments[++i]);
        else if (arguments[i] == "--locale" && !locale && i + 1 < arguments.size()) {
            locale = arguments[++i];
            if (locale->empty())
                throw std::invalid_argument("Locale must not be empty");
        } else
            throw std::invalid_argument("Unknown, repeated or incomplete content resolve option");
    }
    jt::Json report;
    report["format"] = jt::Json(std::string(content::pack_resolver_version));
    int result = 0;
    try {
        const auto selected = mount(arguments[0]);
        std::vector<content::MountedPack> registered;
        for (const auto& path : parents)
            registered.push_back(mount(path));
        const auto pack =
            content::resolve_pack(selected, registered, locale.value_or(""), require_classic);
        report["status"] = jt::Json("resolved");
        report["scope"] = jt::Json("pack-selection-and-gameplay");
        report["profile"] = jt::Json(pack->profile);
        report["defaultLocale"] = jt::Json(pack->default_locale);
        report["selectedLocale"] = jt::Json(pack->selected_locale);
        report["manifests"].setArray();
        for (const auto& receipt : pack->manifests) {
            auto row = identity(receipt.pack);
            row["provenance"]["authors"].setArray();
            for (const auto& author : receipt.provenance.authors)
                row["provenance"]["authors"].getArray().emplace_back(author);
            row["provenance"]["license"] = jt::Json(receipt.provenance.license);
            row["provenance"]["notes"].setArray();
            for (const auto& note : receipt.provenance.notes)
                row["provenance"]["notes"].getArray().emplace_back(note);
            report["manifests"].getArray().push_back(std::move(row));
        }
        report["files"].setArray();
        const auto file = [&](std::string role,
                              const std::shared_ptr<const content::ResolvedFile>& value,
                              std::string language = {}) {
            jt::Json row;
            row["role"] = jt::Json(role);
            if (!language.empty())
                row["locale"] = jt::Json(language);
            if (value) {
                row["owner"] = identity(value->owner);
                row["path"] = jt::Json(value->receipt.path);
                row["sha256"] = jt::Json(value->receipt.sha256);
            } else
                row["externalWorld"] = jt::Json(true);
            report["files"].getArray().push_back(std::move(row));
        };
        for (const auto& [name, document] : pack->components)
            file(name, document.file);
        for (const auto& [name, languages] : pack->localized)
            for (const auto& [language, document] : languages)
                file(name, document.file, language);
        if (pack->compatibility)
            file("compatibility", pack->compatibility->file);
        for (const auto& scenario : pack->scenarios)
            file("scenario:" + scenario.id, scenario.world);
        for (const auto& resource : pack->resources)
            file("resource:" + resource.id, resource.file);
        report["gameplay"]["owner"] = identity(pack->gameplay.owner);
        report["gameplay"]["builtin"] = jt::Json(!pack->gameplay.manifest);
        report["gameplay"]["semanticSha256"] = jt::Json(pack->gameplay.bundle->semantic_sha256);
        report["gameplay"]["manifestSha256"] = jt::Json(pack->gameplay.bundle->manifest_sha256);
        report["gameplay"]["classicValues"] = jt::Json(pack->gameplay.bundle->classic_values);
        report["gameplay"]["mode"] =
            jt::Json(pack->gameplay.bundle->custom_mode ? "custom" : "classic");
        if (pack->gameplay.manifest)
            file("gameplay", pack->gameplay.manifest);
        report["gameplay"]["files"].setArray();
        for (const auto& receipt : pack->gameplay.bundle->files) {
            jt::Json row;
            row["path"] = jt::Json(receipt.path);
            row["sha256"] = jt::Json(receipt.sha256);
            report["gameplay"]["files"].getArray().push_back(std::move(row));
        }
        report["limitations"].setArray();
        report["limitations"].getArray().emplace_back(
            "Rule/text/event bodies, cross-references, world/media bytes, session and "
            "original-peer admission are not validated.");
        if (!as_json)
            std::cout << "Resolved content pack " << pack->manifests.back().pack.id << " revision "
                      << pack->manifests.back().pack.revision << '\n'
                      << "Locale: " << pack->selected_locale
                      << "; manifests: " << pack->manifests.size() << '\n'
                      << report["limitations"][0].getString() << '\n';
    } catch (const content::Error& error) {
        result = error.code == "invalid-json" ? 4 : error.code == "source-error" ? 2 : 1;
        report["status"] = jt::Json("rejected");
        report["code"] = jt::Json(error.code);
        report["location"] = jt::Json(error.location);
        report["message"] = jt::Json(error.what());
        if (!as_json)
            std::cerr << error.code << ": " << error.what() << '\n';
    } catch (const std::exception& error) {
        result = 2;
        report["status"] = jt::Json("rejected");
        report["code"] = jt::Json("input-error");
        report["message"] = jt::Json(error.what());
        if (!as_json)
            std::cerr << error.what() << '\n';
    }
    if (as_json)
        std::cout << report.toString() << '\n';
    std::cout.flush();
    if (!std::cout)
        throw std::runtime_error("Output write failed");
    return result;
}
