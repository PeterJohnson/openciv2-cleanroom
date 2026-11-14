#pragma once
#include "classic_text_reader.hpp"
#include "state/labels.hpp"

namespace openciv::formats::classic {
struct LabelServices {
    // Missing LABELS is a failed result, not a fabricated successful table.
    std::function<void()> missing_labels;
};
// P04 classic resource indices with F03 file/line semantics and owned text.
// No presentation decoding, filesystem discovery or implicit rule loading.
class LabelLoader {
public:
    LabelLoader(TextReader& reader, TextDirectories& directories, state::TextPool& pool,
                state::Labels& labels, LabelServices services = {});
    // Select language and invoke the supplied GAME-cache builder,
    // and initialize <nil>. Label/popup loading is a separate operation.
    void initialize(std::int32_t default_language, const std::function<void()>& build_game_cache);
    // Does not change the selected language or rebuild the GAME cache.
    void rebuild();
    // These standalone loads require an initialized shared pool.
    std::int32_t load_labels();
    void load_popups();

private:
    state::TextId next_string();
    void publish(std::int32_t index, state::TextId id);
    TextReader& reader_;
    TextDirectories& directories_;
    state::TextPool& pool_;
    state::Labels& labels_;
    LabelServices services_;
};
} // namespace openciv::formats::classic
