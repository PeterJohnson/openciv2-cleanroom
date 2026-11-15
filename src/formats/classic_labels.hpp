#pragma once
#include "formats/classic_text_reader.hpp"
#include "state/label_catalog.hpp"

namespace openciv::formats::classic {
struct LabelServices {
    std::function<void()> missing_labels;
};
// Reads an owned catalog, including home-directory fallback. The reader is
// closed on every exit; no runtime pool, language or cache is changed.
state::LabelCatalog load_labels(TextReader& reader, TextDirectories& directories,
                                const LabelServices& services = {});
} // namespace openciv::formats::classic
