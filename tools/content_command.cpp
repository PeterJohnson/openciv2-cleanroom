#include "content_command.hpp"
#include "platform/content_files.hpp"
#include <iostream>

int run_content_command(std::span<const std::string> arguments) {
    namespace content = openciv::formats::content;
    if (arguments.size() < 2 || arguments[0] != "validate")
        throw std::invalid_argument(
            "Usage: oc2 content validate GAMEPLAY_MANIFEST [--require-classic] [--json]");
    bool as_json = false, require_classic = false;
    for (std::size_t i = 2; i < arguments.size(); ++i) {
        if (arguments[i] == "--json" && !as_json)
            as_json = true;
        else if (arguments[i] == "--require-classic" && !require_classic)
            require_classic = true;
        else
            throw std::invalid_argument("Unknown or repeated content option");
    }
    jt::Json report;
    report["format"] = jt::Json("openciv2-gameplay-validation/1");
    int result = 0;
    try {
        const auto manifest = std::filesystem::absolute(
            std::filesystem::path(std::u8string(arguments[1].begin(), arguments[1].end())));
        openciv::io::ContentDirectory directory(manifest.parent_path());
        const auto filename = manifest.filename().generic_u8string();
        const auto source = directory.read(std::string(filename.begin(), filename.end()));
        const auto bundle = content::load_gameplay(
            source.bytes, [&](auto path) { return directory.read(path); }, require_classic);
        report["status"] = jt::Json("validated");
        report["scope"] = jt::Json("gameplay-bundle-only");
        report["id"] = jt::Json(bundle->id);
        report["revision"] = jt::Json(bundle->revision);
        report["mode"] = jt::Json(bundle->custom_mode ? "custom" : "classic");
        report["classicValues"] = jt::Json(bundle->classic_values);
        report["manifestSha256"] = jt::Json(bundle->manifest_sha256);
        report["semanticSha256"] = jt::Json(bundle->semantic_sha256);
        report["files"].setArray();
        for (const auto& file : bundle->files) {
            jt::Json entry;
            entry["path"] = jt::Json(file.path);
            entry["sha256"] = jt::Json(file.sha256);
            report["files"].getArray().push_back(std::move(entry));
        }
        report["limitations"].setArray();
        report["limitations"].getArray().emplace_back(
            "Selected rule references, mechanic bindings, session/checkpoint and original-peer "
            "admission are not checked.");
        if (!as_json)
            std::cout << "Validated gameplay bundle " << bundle->id << " revision "
                      << bundle->revision << '\n'
                      << "Mode: " << (bundle->custom_mode ? "custom" : "classic")
                      << "; classic operands: " << (bundle->classic_values ? "yes" : "no") << '\n'
                      << "Semantic SHA-256: " << bundle->semantic_sha256 << '\n'
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
