export module openciv.formats.classic_city_names;
import openciv.formats.classic_text;

import std;
export import openciv.formats.classic_text_reader;
export import openciv.city.name_catalog;

export namespace openciv::formats::classic {
/// Decode the currently open CITY source. Preserves first-match selectors and
/// incomplete prefixes; the latter fail if selection reaches their unknown end.
state::CityNameLists read_city_names(TextReader& reader, const TextCase& comparison);
/// Snapshot scenario/home sources as ordered layers; each owns its fallback.
city::NameCatalog load_city_names(TextReader& reader, TextDirectories& directories,
                                  const TextCase& comparison);
} // namespace openciv::formats::classic

module :private;
namespace openciv::formats::classic {
state::CityNameLists read_city_names(TextReader& reader, const TextCase& comparison) {
    if (!comparison.equal)
        throw std::invalid_argument("City names require a comparison policy");
    std::vector<std::string> lines;
    std::size_t size = 0;
    while (reader.next_line()) {
        size += reader.line().size() + 1;
        if (size > 16 * 1024 * 1024)
            throw std::invalid_argument("CITY source exceeds 16 MiB");
        lines.emplace_back(reader.line());
    }
    const auto stop = [&](std::string_view line) {
        return line.size() >= 5 && comparison.equal(line.substr(0, 5), "@STOP");
    };
    const auto text = [&](const std::string& bytes) -> state::RuleText {
        return {decode_windows1252(bytes, "CITY"), bytes};
    };
    state::CityNameLists result;
    std::size_t materialized = 0;
    for (std::size_t i = 0; i < lines.size(); ++i) {
        if (lines[i].empty() || lines[i].front() != '@')
            continue;
        state::CityNameList list;
        list.plural = text(lines[i].substr(1));
        std::size_t j = i + 1;
        // Other section headers and comments are names unless a blank or STOP ends this list.
        for (; j < lines.size() && !lines[j].empty() && !stop(lines[j]); ++j) {
            materialized += lines[j].size();
            if (materialized > 16 * 1024 * 1024)
                throw std::invalid_argument("Materialized CITY lists exceed 16 MiB");
            list.names.push_back(text(lines[j]));
        }
        list.complete = j < lines.size() || list.names.size() >= 255;
        if (!result.fallback && comparison.equal(list.plural.classic, "EXTRA")) {
            result.fallback = list.names;
        }
        result.civilizations.push_back(std::move(list));
    }
    return result;
}
city::NameCatalog load_city_names(TextReader& reader, TextDirectories& directories,
                                  const TextCase& comparison) {
    if (!comparison.equal)
        throw std::invalid_argument("City names require a comparison policy");
    const auto saved_search = directories.search;
    std::vector<state::CityNameLists> layers;
    try {
        if (reader.open_source(saved_search, "CITY"))
            layers.push_back(read_city_names(reader, comparison));
        reader.close();
        if (saved_search != directories.home && reader.open_source(directories.home, "CITY"))
            layers.push_back(read_city_names(reader, comparison));
        reader.close();
    } catch (...) {
        directories.search = saved_search;
        directories.working = directories.home;
        try {
            reader.close();
        } catch (...) {
        }
        throw;
    }
    directories.search = saved_search;
    directories.working = directories.home;
    return city::NameCatalog(std::move(layers), comparison.equal);
}
} // namespace openciv::formats::classic
