#include "content_command.hpp"
#include "formats/content_conversion.hpp"
#include "platform/content_files.hpp"
#include "platform/native_files.hpp"
#include <iostream>

namespace {
namespace content = openciv::formats::content;
std::filesystem::path path(std::string_view text) {
    return std::filesystem::path(std::u8string(text.begin(), text.end()));
}
content::MountedPack mount(std::string_view filename) {
    const auto absolute = std::filesystem::absolute(path(filename));
    auto directory = std::make_shared<openciv::io::ContentDirectory>(absolute.parent_path());
    const auto leaf = absolute.filename().generic_u8string();
    return {directory->read(std::string(leaf.begin(), leaf.end())),
            [directory](std::string_view scope, std::string_view relative) {
                return directory->read(scope.empty()
                                           ? std::string(relative)
                                           : std::string(scope) + "/" + std::string(relative),
                                       content::ResolutionLimits{}.maximum_bytes, scope);
            }};
}
} // namespace
int run_content_convert(std::span<const std::string> arguments, bool city_names) {
    if (arguments.size() < 3)
        throw std::invalid_argument("Usage: oc2 content convert-names|convert-preferences SOURCE "
                                    "CONTENT_MANIFEST NEW_OUTPUT [--parent MANIFEST]...");
    std::vector<std::string> parents;
    for (std::size_t i = 3; i < arguments.size(); ++i) {
        if (arguments[i] == "--parent" && i + 1 < arguments.size())
            parents.push_back(arguments[++i]);
        else
            throw std::invalid_argument("Unknown or incomplete conversion option");
    }
    jt::Json report;
    report["format"] = "openciv2-content-conversion/1";
    int status = 0;
    try {
        const auto input = std::filesystem::absolute(path(arguments[0]));
        const auto output = std::filesystem::absolute(path(arguments[2]));
        if (std::filesystem::exists(output) ||
            std::filesystem::weakly_canonical(input) == std::filesystem::weakly_canonical(output))
            throw std::runtime_error("Conversion requires a new output distinct from its source");
        openciv::io::ContentDirectory directory(input.parent_path());
        const auto leaf = input.filename().generic_u8string();
        const auto source = directory.read(std::string(leaf.begin(), leaf.end()));
        const auto selected = mount(arguments[1]);
        std::vector<content::MountedPack> registered;
        for (const auto& parent : parents)
            registered.push_back(mount(parent));
        const auto pack = content::resolve_pack(selected, registered);
        auto converted =
            city_names ? content::convert_city_names(source, *pack,
                                                     openciv::formats::classic::ascii_text_case())
                       : content::convert_city_preferences(
                             source, *pack, openciv::formats::classic::ascii_text_case());
        report = std::move(converted.receipt);
        const auto destination = output.generic_u8string();
        report["output"]["path"] = std::string(destination.begin(), destination.end());
        report["selectedPacks"].setArray();
        for (const auto& selected_pack : pack->manifests) {
            jt::Json row;
            row["id"] = selected_pack.pack.id;
            row["revision"] = selected_pack.pack.revision;
            row["sha256"] = selected_pack.pack.sha256;
            row["source"] = selected_pack.source_identity;
            report["selectedPacks"].getArray().push_back(std::move(row));
        }
        openciv::io::write_new_file(
            output, {reinterpret_cast<const std::uint8_t*>(converted.component.data()),
                     converted.component.size()});
        report["status"] = "converted";
    } catch (const content::Error& error) {
        status = error.code == "source-error" ? 2 : error.code == "invalid-json" ? 4 : 1;
        report["status"] = "rejected";
        report["code"] = error.code;
        report["location"] = error.location;
        report["message"] = error.what();
    } catch (const std::exception& error) {
        status = 2;
        report["status"] = "rejected";
        report["code"] = "input-output-error";
        report["message"] = error.what();
    }
    std::cout << report.toString() << '\n';
    std::cout.flush();
    if (!std::cout)
        throw std::runtime_error("Conversion receipt write failed");
    return status;
}
