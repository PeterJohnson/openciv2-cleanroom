#pragma once
#include "state/labels.hpp"
#include <string>

namespace openciv::state {
// Owned text, independent of a source format or the runtime text pool.
struct LabelCatalog {
    std::int32_t declared_count = 0;
    std::vector<std::string> entries;
    std::array<std::optional<std::string>, 3> popups;
};
void install_labels(const LabelCatalog& input, TextPool& pool, Labels& labels);
} // namespace openciv::state
