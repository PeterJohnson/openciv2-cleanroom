#pragma once
#include "state/city_names.hpp"
#include "base/text_comparison.hpp"

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
