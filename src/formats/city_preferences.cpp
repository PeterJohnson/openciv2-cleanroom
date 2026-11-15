#include "city_preferences.hpp"
#include "content_reader.hpp"
#include <algorithm>

namespace openciv::formats::content {
std::shared_ptr<const CityPreferenceContent>
load_city_preferences(const ResolvedPack& pack, const state::RuleDefinitions& rules,
                      const base::NameComparison& comparison) {
    if (!comparison)
        throw std::invalid_argument("City preferences require a name comparison service");
    const auto selected = pack.components.find("cityPreferences");
    if (selected == pack.components.end())
        throw Error("invalid-content", "cityPreferences", "missing selected component");
    auto document = selected->second.json;
    detail::Reader root{document.value, document.numbers, "cityPreferences"};
    root.fields({"format", "humanCityDefensePriority", "buildPreferences"}, true);
    root.at("format").constant("openciv2-city-preferences/1");
    auto out = std::make_shared<CityPreferenceContent>();
    out->source = selected->second.file;
    const auto priority = root.at("humanCityDefensePriority").text();
    if (priority != "deprioritize" && priority != "unchanged")
        root.at("humanCityDefensePriority").error("unknown defense preference update");
    out->update.deprioritize_defense = priority == "deprioritize";
    const auto list = root.at("buildPreferences");
    if (list.array_size() > 32)
        list.error("at most 32 build preferences are supported");
    const auto& improvements = rules.improvements;
    for (std::size_t i = 0; i < list.array_size(); ++i) {
        const auto field = list.element(i);
        const auto id = field.id();
        const auto found = std::find_if(improvements.begin() + 1, improvements.begin() + 39,
                                        [&](const auto& row) { return row.identity.id == id; });
        if (found == improvements.begin() + 39)
            field.error("build preference must resolve to improvement slot 1..38");
        const auto slot = static_cast<std::size_t>(found - improvements.begin());
        const auto& name = found->identity.name.classic;
        if (name.empty() || name == " ")
            field.error("improvement name cannot round-trip through a trimmed classic token");
        for (std::size_t earlier = 1; earlier < slot; ++earlier) {
            try {
                if (comparison(name, improvements[earlier].identity.name.classic))
                    field.error("classic preference name resolves to an earlier improvement");
            } catch (const base::ComparisonUnavailable& error) {
                throw Error("comparison-unavailable", field.path, error.what());
            }
        }
        out->update.build_order.push_back(static_cast<std::uint8_t>(slot));
    }
    return out;
}
} // namespace openciv::formats::content
