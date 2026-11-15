export module openciv.state.labels;

import std;
export import openciv.state.text_pool;

export extern "C++" {
    namespace openciv::state {
    // P04 classic resource indices, independent of text storage. Missing content is not
    // an implicit label zero. Construction supplies only the specified defaults.
    struct Labels {
        static constexpr std::size_t capacity = 888;
        std::int32_t status_language = 0;
        std::int32_t declared_count = 0;
        std::vector<std::optional<TextId>> entries;
        std::array<std::optional<TextId>, 3> popups{};

        std::string_view label(std::size_t index, const TextPool& pool) const {
            if (index >= entries.size() || !entries[index])
                throw std::out_of_range("Label has no established table backing");
            return pool.text(*entries[index]);
        }
        std::string_view popup(std::size_t index, const TextPool& pool) const {
            static constexpr std::array<std::string_view, 3> defaults{"OK", "Help", "Cancel"};
            return popups.at(index) ? pool.text(*popups[index]) : defaults[index];
        }
    };
    } // namespace openciv::state
}
