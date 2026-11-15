export module openciv.state.city_names;

import std;
export import openciv.state.rules;

export namespace openciv::state {
/// data-format.md section 5. Selectors are live plural names, never tribe IDs.
struct CityNameList {
    RuleText plural;
    std::vector<RuleText> names;
    // Imported prefixes may be usable without establishing a valid end.
    // Selecting beyond such a prefix reports unsupported content.
    bool complete = true;
};
/// Civilization name lists and an optional fallback from one content layer.
struct CityNameLists {
    std::vector<CityNameList> civilizations;
    // Missing and present-but-exhausted remain distinct.
    std::optional<std::vector<RuleText>> fallback;
};
} // namespace openciv::state
