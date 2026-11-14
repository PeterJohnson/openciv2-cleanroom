#include "formats/content_conversion.hpp"
#include "formats/city_preferences.hpp"
#include "formats/classic_text.hpp"
#include "base/hash.hpp"

namespace openciv::formats::content {
namespace {
jt::Json empty_array() {
    jt::Json result;
    result.setArray();
    return result;
}
jt::Json file_receipt(const Source& source) {
    jt::Json out;
    out["identity"] = source.identity;
    out["sha256"] = base::sha256(source.bytes);
    return out;
}
void add_dependency(jt::Json& out, std::string_view role,
                    const std::shared_ptr<const ResolvedFile>& source) {
    if (!source)
        return; // In-memory callers can supply authored resolved documents.
    jt::Json row;
    row["role"] = std::string(role);
    row["identity"] = source->receipt.identity;
    row["path"] = source->receipt.path;
    row["sha256"] = source->receipt.sha256;
    row["owner"]["id"] = source->owner.id;
    row["owner"]["revision"] = source->owner.revision;
    row["owner"]["sha256"] = source->owner.sha256;
    out.getArray().push_back(std::move(row));
}
} // namespace
ContentConversion convert_city_preferences(const Source& source, const ResolvedPack& selected,
                                           const classic::TextCase& comparison) {
    if (source.identity.empty() || source.bytes.size() > 16 * 1024 * 1024)
        throw Error("source-error", source.identity,
                    "conversion requires an identified source of at most 16 MiB");
    const auto rules = load_rules(selected);
    const std::string text(source.bytes.begin(), source.bytes.end());
    // Validate encoding independently of case equivalence. This does not supply
    // an unestablished non-ASCII comparison table.
    decode_windows1252(text, source.identity);
    classic::TextWorkspace workspace;
    classic::TextDirectories directories;
    classic::TextFiles files{[&](std::string_view, std::string_view name) -> classic::OpenedText {
                                 if (name != "CITYPREF.TXT")
                                     throw std::logic_error("Unexpected conversion source request");
                                 return {source.identity,
                                         std::make_unique<classic::MemoryTextStream>(text)};
                             },
                             {}};
    classic::TextReader reader(workspace, directories, std::move(files), comparison);
    state::CityPreferences parsed;
    try {
        classic::load_city_preferences(reader, rules->definitions, parsed, comparison);
        reader.close();
        // A conversion cannot silently turn a misspelled reference into a shorter
        // authored list. Classic loading keeps its existing stop-at-miss behavior.
        if (reader.open("CITYPREF.TXT", "AUTOBUILD")) {
            for (int i = 0; i < 32 && reader.next_line(); ++i) {
                if (!reader.line().empty() && reader.line().front() == '@')
                    break;
                const auto token = reader.next_token();
                if (token.empty() || token.front() == ';')
                    break; // F03: blank/comment lines terminate, rather than skip, the list.
                bool found = false;
                for (std::size_t slot = 1; slot <= 38; ++slot)
                    if (comparison.equal(
                            token, rules->definitions.improvements[slot].identity.name.classic)) {
                        found = true;
                        break;
                    }
                if (!found)
                    throw Error("unresolved-classic-reference", source.identity,
                                "AUTOBUILD entry does not resolve to improvement slot 1..38");
            }
        }
        reader.close();
    } catch (const classic::TextError& error) {
        throw Error(error.fault == classic::TextFault::comparison_unavailable
                        ? "comparison-unavailable"
                        : "unsupported-classic-text",
                    source.identity, error.what());
    }
    jt::Json component;
    component["format"] = "openciv2-city-preferences/1";
    component["humanCityDefensePriority"] = parsed.no_defend ? "deprioritize" : "unchanged";
    component["buildPreferences"] = empty_array();
    for (std::int32_t i = 0; i < parsed.count; ++i) {
        const auto slot = static_cast<std::size_t>(parsed.build_order[static_cast<std::size_t>(i)]);
        component["buildPreferences"].getArray().emplace_back(
            rules->definitions.improvements[slot].identity.id);
    }
    ContentConversion out;
    out.component = component.toString() + "\n";
    auto verification = selected;
    verification.components["cityPreferences"] = {
        nullptr, parse_json(out.component, "converted-city-preferences")};
    const auto native = load_city_preferences(verification, comparison);
    if (native->update.deprioritize_defense != (parsed.no_defend != 0) ||
        native->update.build_order.size() != static_cast<std::size_t>(parsed.count))
        throw std::logic_error("Converted preference semantics disagree with classic input");
    for (std::size_t i = 0; i < native->update.build_order.size(); ++i)
        if (native->update.build_order[i] != parsed.build_order[i])
            throw std::logic_error("Converted preference slot order disagrees with classic input");
    auto& receipt = out.receipt;
    receipt["format"] = "openciv2-content-conversion/1";
    receipt["conversion"] = "classic-city-preferences/1";
    receipt["profile"] = selected.profile;
    receipt["source"] = file_receipt(source);
    receipt["output"]["format"] = "openciv2-city-preferences/1";
    receipt["output"]["sha256"] = base::sha256(
        {reinterpret_cast<const std::uint8_t*>(out.component.data()), out.component.size()});
    receipt["dependencies"] = empty_array();
    add_dependency(receipt["dependencies"], "rules", rules->rules_source);
    add_dependency(receipt["dependencies"], "compatibility", rules->compatibility_source);
    receipt["gameplaySemanticSha256"] = selected.gameplay.bundle->semantic_sha256;
    receipt["unsupportedFields"] = empty_array();
    receipt["lossyFields"] = empty_array();
    receipt["limitations"] = empty_array();
    for (const char* note : {"Component output, not a complete playable pack.",
                             "Semantic conversion preserves the first 32 AUTOBUILD entries; "
                             "classic comments, formatting and unused sections are not exported.",
                             "ASCII folding and exact byte equality only; non-ASCII comparison "
                             "equivalence is not established.",
                             "Absent NODEFEND preserves the caller's prior NoDefend value."})
        receipt["limitations"].getArray().emplace_back(note);
    return out;
}
} // namespace openciv::formats::content
