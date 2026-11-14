#include "classic_labels.hpp"
#include "base/arithmetic.hpp"

namespace openciv::formats::classic {
LabelLoader::LabelLoader(TextReader& reader, TextDirectories& directories, state::TextPool& pool,
                         state::Labels& labels, LabelServices services)
    : reader_(reader), directories_(directories), pool_(pool), labels_(labels),
      services_(std::move(services)) {}

void LabelLoader::initialize(std::int32_t default_language,
                             const std::function<void()>& build_game_cache) {
    if (!build_game_cache)
        throw std::invalid_argument("Text initialization requires a GAME-cache service");
    labels_.status_language =
        static_cast<std::int32_t>(base::WrappingI32{default_language} - base::WrappingI32{1});
    if (labels_.status_language > 2)
        labels_.status_language = 0;
    static constexpr std::array<std::string_view, 5> extensions{"TXT", "FRE", "GER", "ITA", "SPA"};
    directories_.extension = default_language >= 1 && default_language <= 5
                                 ? extensions[static_cast<std::size_t>(default_language - 1)]
                                 : "TXT";
    build_game_cache();
    pool_.initialize();
}
void LabelLoader::rebuild() {
    if (!pool_.size())
        pool_.initialize();
    labels_.entries.clear();
    load_labels();
    load_popups();
}
state::TextId LabelLoader::next_string() {
    reader_.next_line();
    return pool_.store(reader_.line());
}
void LabelLoader::publish(std::int32_t index, state::TextId id) {
    // Bound classic resource indices before publishing an entry.
    if (index < 0 || static_cast<std::size_t>(index) >= state::Labels::capacity ||
        static_cast<std::size_t>(index) >= labels_.entries.size())
        throw std::out_of_range("Label publication exceeds established table backing");
    labels_.entries[static_cast<std::size_t>(index)] = id;
}
std::int32_t LabelLoader::load_labels() {
    if (!pool_.size())
        throw std::invalid_argument("Label loading requires an initialized shared text pool");
    std::int32_t index = 0;
    if (reader_.open("LABELS", "LABELS")) {
        reader_.next_line();
        labels_.declared_count = reader_.next_integer();
        labels_.entries.assign(state::Labels::capacity, std::nullopt);
        while (index < labels_.declared_count) {
            const auto id = next_string();
            publish(index, id);
            ++index;
        }
        if (index < static_cast<std::int32_t>(state::Labels::capacity)) {
            // The directory is a scoped search override, including safe-stop
            // exceptions. File/buffer/pool publications are not rolled back.
            struct Restore {
                std::string& target;
                std::string saved;
                ~Restore() { target.swap(saved); }
            } restore{directories_.search, directories_.search};
            directories_.search = directories_.home;
            if (reader_.open("LABELS", "LABELS")) {
                reader_.next_line();
                const auto fallback_count = reader_.next_integer();
                for (std::int32_t skipped = 0; skipped < labels_.declared_count; ++skipped)
                    reader_.next_line();
                while (index < fallback_count) {
                    const auto id = next_string();
                    publish(index, id);
                    ++index;
                }
            }
        }
        if (index < static_cast<std::int32_t>(state::Labels::capacity)) {
            const auto error = pool_.store("[ERROR:LABELS.TXT]");
            while (index < static_cast<std::int32_t>(state::Labels::capacity))
                publish(index++, error);
        }
    } else if (services_.missing_labels) {
        services_.missing_labels();
    }
    reader_.close();
    if (index <= 0) {
        labels_.entries.clear();
        labels_.declared_count = 0;
    }
    return index;
}
void LabelLoader::load_popups() {
    if (!pool_.size())
        throw std::invalid_argument("Popup loading requires an initialized shared text pool");
    if (reader_.open("LABELS", "POPUPS"))
        for (std::size_t i = 0; i < labels_.popups.size(); ++i)
            labels_.popups[i] = next_string();
}
} // namespace openciv::formats::classic
