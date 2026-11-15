import std;
import openciv.tools.content_command;
import openciv.formats.content_pack;
import openciv.formats.city_preferences;
import openciv.formats.city_names;
import openciv.formats.rules;
import openciv.rules.rules_install;
import openciv.platform.content_files;

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
int run_content_resolve(std::span<const std::string> arguments, ContentValidation validation) {
    const bool validate_rules = validation != ContentValidation::selection;
    const bool validate_preferences = validation == ContentValidation::city_preferences;
    const bool validate_names = validation == ContentValidation::city_names;
    if (arguments.empty())
        throw std::invalid_argument(
            "Usage: oc2 content resolve|validate-rules|validate-preferences|validate-names "
            "CONTENT_MANIFEST "
            "[--parent "
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
            throw std::invalid_argument("Unknown, repeated or incomplete content selection option");
    }
    jt::Json report;
    report["format"] = jt::Json(validate_names         ? "openciv2-city-names-validation/1"
                                : validate_preferences ? "openciv2-city-preferences-validation/1"
                                : validate_rules       ? "openciv2-rules-validation/1"
                                                 : std::string(content::pack_resolver_version));
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
        if (validate_rules) {
            const auto rules = content::load_rules(*pack);
            const auto preferences =
                validate_preferences
                    ? content::load_city_preferences(*pack, rules->input.definitions,
                                                     openciv::base::ascii_name_equal)
                    : nullptr;
            const auto names = validate_names
                                   ? content::load_city_names(*pack, rules->input.definitions,
                                                              openciv::base::ascii_name_equal)
                                   : nullptr;
            auto installed = std::make_unique<openciv::state::RuleState>();
            installed->text.initialize();
            openciv::rules::RuleInstaller(rules->input, *installed).load(true);
            const auto& definitions = installed->definitions;
            using openciv::state::CosmicParameter;
            report["status"] = jt::Json("validated");
            report["scope"] = jt::Json("rules-and-gameplay-references");
            if (preferences) {
                report["scope"] = jt::Json("rules-gameplay-and-city-preferences");
                report["cityPreferences"]["humanCityDefensePriority"] = jt::Json(
                    preferences->update.deprioritize_defense ? "deprioritize" : "unchanged");
                report["cityPreferences"]["buildSlots"].setArray();
                for (const auto slot : preferences->update.build_order)
                    report["cityPreferences"]["buildSlots"].getArray().emplace_back(slot);
                report["cityPreferences"]["comparison"] =
                    jt::Json("ascii-with-exact-byte-equality");
            }
            if (names) {
                report["scope"] = jt::Json("rules-gameplay-and-city-names");
                auto& values = report["cityNames"];
                values["comparison"] = jt::Json("ascii-with-exact-byte-equality");
                values["startingListsComplete"] = jt::Json(names->starting_lists_complete);
                values["fallbackAvailable"] =
                    jt::Json(names->catalog.layers().front().fallback.has_value());
                values["fallbackCount"] = jt::Json(
                    names->catalog.layers().front().fallback
                        ? static_cast<int>(names->catalog.layers().front().fallback->size())
                        : 0);
                values["civilizations"].setArray();
                for (const auto& list : names->catalog.layers().front().civilizations) {
                    jt::Json row;
                    row["pluralName"] = jt::Json(list.plural.utf8);
                    row["nameCount"] = jt::Json(static_cast<int>(list.names.size()));
                    values["civilizations"].getArray().push_back(std::move(row));
                }
            }
            report["rules"]["enabledAdvances"] = jt::Json(definitions.enabled_advance_count);
            report["rules"]["roadMovementMultiplier"] =
                jt::Json(definitions.cosmic[CosmicParameter::road_movement_multiplier]);
            report["rules"]["foodBoxRows"] =
                jt::Json(definitions.cosmic[CosmicParameter::food_box_rows]);
            report["rules"]["shieldBoxRows"] =
                jt::Json(definitions.cosmic[CosmicParameter::shield_box_rows]);
            report["rules"]["governmentAdvances"].setArray();
            for (const auto advance : rules->input.government_advances)
                report["rules"]["governmentAdvances"].getArray().push_back(
                    advance ? jt::Json(static_cast<int>(*advance)) : jt::Json());
            report["rules"]["units"].setArray();
            for (const auto& unit : definitions.units) {
                jt::Json row;
                row["id"] = jt::Json(unit.identity.id);
                row["movement"] = jt::Json(unit.movement);
                row["hitPoints"] = jt::Json(unit.hit_points);
                row["costRows"] = jt::Json(unit.cost_rows);
                report["rules"]["units"].getArray().push_back(std::move(row));
            }
        }
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
            validate_names
                ? "Unestablished non-ASCII comparisons reject. Starting-list coverage is required "
                  "for base packs and reported for overlays. Full classic export representability, "
                  "preferences, text/events, world/media bytes, session and original-peer "
                  "admission "
                  "are not validated."
            : validate_preferences
                ? "Unestablished non-ASCII name comparisons reject. Full classic export "
                  "representability, city names, text/events, compatibility metadata, world/media "
                  "bytes, session and original-peer admission are not validated."
            : validate_rules
                ? "City names/preferences, text/events, compatibility metadata, world/media bytes, "
                  "session and original-peer admission are not validated."
                : "Rule/text/event bodies, cross-references, world/media bytes, session and "
                  "original-peer admission are not validated.");
        if (!as_json)
            std::cout << (validate_names         ? "Validated city names and rules for "
                          : validate_preferences ? "Validated city preferences and rules for "
                          : validate_rules       ? "Validated rule definitions for "
                                                 : "Resolved content pack ")
                      << pack->manifests.back().pack.id << " revision "
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
