export module openciv.city.name_catalog;

import std;
export import openciv.state.city_names;
export import openciv.base.text_comparison;

export extern "C++" {
    namespace openciv::city {
    enum class NameOutcome { unavailable, exhausted, selected };
    struct NameAttempt {
        NameOutcome outcome = NameOutcome::unavailable;
        std::string bytes;
    };
    // Owned data, independent of source syntax and I/O. Earlier layers take precedence;
    // a matching list uses only the fallback from its own layer.
    class NameCatalog {
    public:
        NameCatalog(std::vector<state::CityNameLists> layers, base::NameComparison comparison);
        NameAttempt select(std::string_view plural, std::uint8_t remaining) const;
        const std::vector<state::CityNameLists>& layers() const noexcept { return layers_; }

    private:
        std::vector<state::CityNameLists> layers_;
        base::NameComparison comparison_;
    };
    } // namespace openciv::city
}
