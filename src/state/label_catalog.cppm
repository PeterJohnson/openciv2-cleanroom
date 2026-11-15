export module openciv.state.label_catalog;

import std;
export import openciv.state.labels;

export namespace openciv::state {
// Owned text, independent of a source format or the runtime text pool.
struct LabelCatalog {
    std::int32_t declared_count = 0;
    std::vector<std::string> entries;
    std::array<std::optional<std::string>, 3> popups;
};
void install_labels(const LabelCatalog& input, TextPool& pool, Labels& labels);
} // namespace openciv::state

module :private;
namespace openciv::state {
void install_labels(const LabelCatalog& input, TextPool& pool, Labels& labels) {
    if (input.entries.size() > Labels::capacity)
        throw std::invalid_argument("Label catalog exceeds the supported index range");
    if (!pool.size())
        pool.initialize();
    Labels replacement = labels;
    replacement.declared_count = input.declared_count;
    replacement.entries.clear();
    std::map<std::string, TextId> interned;
    const auto store = [&](const std::string& value) {
        auto [entry, inserted] = interned.try_emplace(value);
        if (inserted)
            entry->second = pool.store(value);
        return entry->second;
    };
    for (const auto& value : input.entries)
        replacement.entries.push_back(store(value));
    for (std::size_t i = 0; i < input.popups.size(); ++i)
        if (input.popups[i])
            replacement.popups[i] = store(*input.popups[i]);
    labels = std::move(replacement);
}
} // namespace openciv::state
