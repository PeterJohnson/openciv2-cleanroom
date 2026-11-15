export module openciv.formats.city_names;
import openciv.formats.classic_text;
import openciv.formats.content_reader;

import std;
export import openciv.city.name_catalog;
export import openciv.formats.content_pack;

export namespace openciv::formats::content {
/// Resolved city-name lists together with their content provenance.
struct CityNameContent {
    city::NameCatalog catalog;
    std::shared_ptr<const ResolvedFile> source;
    bool starting_lists_complete = false;
};
/// Validates against rules already selected by the caller; does not reload them.
std::shared_ptr<const CityNameContent> load_city_names(const ResolvedPack& pack,
                                                       const state::RuleDefinitions& rules,
                                                       base::NameComparison comparison);
} // namespace openciv::formats::content

module :private;
namespace openciv::formats::content {
namespace {
bool equal(const base::NameComparison& comparison, std::string_view a, std::string_view b,
           std::string_view path) {
    try {
        return comparison(a, b);
    } catch (const base::ComparisonUnavailable& error) {
        throw Error("comparison-unavailable", std::string(path), error.what());
    }
}
state::RuleText text(detail::Reader field, const base::NameComparison& comparison, bool city_name) {
    auto unicode = field.text();
    auto bytes = encode_windows1252(unicode, field.path);
    if (bytes.empty() || bytes.size() > 254 ||
        bytes.find_first_of(std::string("\0\r\n\x1a", 4)) != std::string::npos)
        field.error("city-name text must fit one nonempty classic line");
    if (bytes.front() == ' ' || bytes.front() == '\t' || bytes.back() == ' ' ||
        bytes.back() == '\t')
        field.error("city-name text would change under classic edge trimming");
    if (city_name && bytes.size() >= 5 &&
        equal(comparison, bytes.substr(0, 5), "@STOP", field.path))
        field.error("city name would terminate a classic name list");
    return {std::move(unicode), std::move(bytes)};
}
std::vector<state::RuleText> names(detail::Reader field, const base::NameComparison& comparison) {
    std::vector<state::RuleText> out;
    for (std::size_t i = 0; i < field.array_size(); ++i)
        out.push_back(text(field.element(i), comparison, true));
    return out;
}
} // namespace
std::shared_ptr<const CityNameContent> load_city_names(const ResolvedPack& pack,
                                                       const state::RuleDefinitions& rules,
                                                       base::NameComparison comparison) {
    if (!comparison)
        throw std::invalid_argument("City names require a comparison service");
    const auto selected = pack.components.find("cityNames");
    if (selected == pack.components.end())
        throw Error("invalid-content", "cityNames", "missing selected component");
    auto document = selected->second.json;
    detail::Reader root{document.value, document.numbers, "cityNames"};
    root.fields({"format", "civilizations", "fallbackNames"}, true);
    root.at("format").constant("openciv2-city-names/1");
    state::CityNameLists parsed;
    bool complete = true;
    const auto lists = root.at("civilizations");
    for (std::size_t i = 0; i < lists.array_size(); ++i) {
        const auto entry = lists.element(i);
        entry.fields({"pluralName", "cityNames"});
        state::CityNameList list{text(entry.at("pluralName"), comparison, false),
                                 names(entry.at("cityNames"), comparison)};
        for (const auto& previous : parsed.civilizations)
            if (equal(comparison, previous.plural.classic, list.plural.classic, entry.path))
                entry.error("ambiguous civilization plural name");
        parsed.civilizations.push_back(std::move(list));
    }
    if (!root.at("fallbackNames").value.isNull())
        parsed.fallback = names(root.at("fallbackNames"), comparison);
    complete = true;
    for (const auto& tribe : rules.tribes) {
        const auto found = std::find_if(
            parsed.civilizations.begin(), parsed.civilizations.end(), [&](const auto& list) {
                return equal(comparison, tribe.plural.classic, list.plural.classic, lists.path);
            });
        if (found == parsed.civilizations.end() || found->names.empty())
            complete = false;
    }
    if (pack.manifests.size() <= 1 && !complete)
        lists.error("a complete base requires a nonempty list for every starting tribe plural");
    return std::make_shared<CityNameContent>(
        CityNameContent{city::NameCatalog({std::move(parsed)}, std::move(comparison)),
                        selected->second.file, complete});
}
} // namespace openciv::formats::content
