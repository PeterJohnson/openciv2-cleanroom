#include "city_names.hpp"
#include "classic_text.hpp"
#include "content_reader.hpp"
#include <algorithm>

namespace openciv::formats::classic {
city::NameProvider city_name_provider(TextReader& reader, TextDirectories& directories,
                                      TextCase comparison) {
    if (!comparison.equal)
        throw std::invalid_argument("City names require a comparison service");
    return {[&reader, &directories, comparison = std::move(comparison)](
                std::string_view plural, std::uint8_t count) -> city::NameAttempt {
                const std::string section(plural);
                directories.working = directories.search;
                bool found = reader.open_low("CITY", section);
                if (!found) {
                    directories.working = directories.home;
                    const auto saved_search = directories.search;
                    directories.search = directories.home;
                    try {
                        found = reader.open_low("CITY", section);
                    } catch (...) {
                        directories.search = saved_search;
                        throw;
                    }
                    directories.search = saved_search;
                }
                if (!found)
                    return {};
                directories.working = directories.home;
                unsigned remaining = count;
                auto scan = [&] {
                    while (remaining > 0) {
                        reader.next_line(); // EOF's retained line, not the boolean, is tested.
                        const auto line = std::string(reader.line());
                        if (line.empty() ||
                            (line.size() >= 5 && comparison.equal(line.substr(0, 5), "@STOP")))
                            break;
                        --remaining;
                    }
                };
                scan();
                if (remaining > 0) {
                    if (!reader.open_low(std::nullopt, "EXTRA"))
                        return {city::NameOutcome::exhausted, {}};
                    scan();
                }
                return remaining > 0 ? city::NameAttempt{city::NameOutcome::exhausted, {}}
                                     : city::NameAttempt{city::NameOutcome::selected,
                                                         std::string(reader.line())};
            },
            [&reader] { reader.close(); }};
}
} // namespace openciv::formats::classic

namespace openciv::formats::content {
namespace {
bool equal(const classic::TextCase& comparison, std::string_view a, std::string_view b,
           std::string_view path) {
    try {
        return comparison.equal(a, b);
    } catch (const classic::TextError& error) {
        if (error.fault != classic::TextFault::comparison_unavailable)
            throw;
        throw Error("comparison-unavailable", std::string(path), error.what());
    }
}
state::RuleText text(detail::Reader field, const classic::TextCase& comparison, bool city_name) {
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
std::vector<state::RuleText> names(detail::Reader field, const classic::TextCase& comparison) {
    std::vector<state::RuleText> out;
    for (std::size_t i = 0; i < field.array_size(); ++i)
        out.push_back(text(field.element(i), comparison, true));
    return out;
}
} // namespace
std::shared_ptr<const CityNameContent> load_city_names(const ResolvedPack& pack,
                                                       const classic::TextCase& comparison) {
    if (!comparison.equal)
        throw std::invalid_argument("City names require a comparison service");
    const auto selected = pack.components.find("cityNames");
    if (selected == pack.components.end())
        throw Error("invalid-content", "cityNames", "missing selected component");
    auto document = selected->second.json;
    detail::Reader root{document.value, document.numbers, "cityNames"};
    root.fields({"format", "civilizations", "fallbackNames"}, true);
    root.at("format").constant("openciv2-city-names/1");
    auto out = std::make_shared<CityNameContent>();
    out->rules = load_rules(pack);
    out->source = selected->second.file;
    const auto lists = root.at("civilizations");
    for (std::size_t i = 0; i < lists.array_size(); ++i) {
        const auto entry = lists.element(i);
        entry.fields({"pluralName", "cityNames"});
        state::CityNameList list{text(entry.at("pluralName"), comparison, false),
                                 names(entry.at("cityNames"), comparison)};
        for (const auto& previous : out->lists.civilizations)
            if (equal(comparison, previous.plural.classic, list.plural.classic, entry.path))
                entry.error("ambiguous civilization plural name");
        out->lists.civilizations.push_back(std::move(list));
    }
    if (!root.at("fallbackNames").value.isNull())
        out->lists.fallback = names(root.at("fallbackNames"), comparison);
    out->starting_lists_complete = true;
    for (const auto& tribe : out->rules->definitions.tribes) {
        const auto found = std::find_if(out->lists.civilizations.begin(),
                                        out->lists.civilizations.end(), [&](const auto& list) {
                                            return equal(comparison, tribe.plural.classic,
                                                         list.plural.classic, lists.path);
                                        });
        if (found == out->lists.civilizations.end() || found->names.empty())
            out->starting_lists_complete = false;
    }
    if (pack.manifests.size() <= 1 && !out->starting_lists_complete)
        lists.error("a complete base requires a nonempty list for every starting tribe plural");
    return out;
}
city::NameProvider city_name_provider(std::shared_ptr<const CityNameContent> content,
                                      classic::TextCase comparison) {
    if (!content || !comparison.equal)
        throw std::invalid_argument("City naming requires content and a comparison service");
    return {[content = std::move(content), comparison = std::move(comparison)](
                std::string_view plural, std::uint8_t remaining) -> city::NameAttempt {
                const auto& lists = content->lists;
                const auto found = std::find_if(
                    lists.civilizations.begin(), lists.civilizations.end(), [&](const auto& list) {
                        return comparison.equal(plural, list.plural.classic);
                    });
                if (found == lists.civilizations.end())
                    return {};
                if (!remaining)
                    return {city::NameOutcome::selected, "@" + found->plural.classic};
                const auto offset = static_cast<std::size_t>(remaining - 1);
                if (offset < found->names.size())
                    return {city::NameOutcome::selected, found->names[offset].classic};
                const auto extra = offset - found->names.size();
                if (lists.fallback && extra < lists.fallback->size())
                    return {city::NameOutcome::selected, (*lists.fallback)[extra].classic};
                return {city::NameOutcome::exhausted, {}};
            },
            [] {}};
}
} // namespace openciv::formats::content
