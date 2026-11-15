export module openciv.formats.city_names;

import std;
export import openciv.city.name_catalog;
export import openciv.formats.content_pack;

export extern "C++" {
    namespace openciv::formats::content {
    struct CityNameContent {
        city::NameCatalog catalog;
        std::shared_ptr<const ResolvedFile> source;
        bool starting_lists_complete = false;
    };
    // Validates against rules already selected by the caller; does not reload them.
    std::shared_ptr<const CityNameContent> load_city_names(const ResolvedPack& pack,
                                                           const state::RuleDefinitions& rules,
                                                           base::NameComparison comparison);
    } // namespace openciv::formats::content
}
