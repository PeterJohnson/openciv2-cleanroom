#pragma once
#include "base/callback_ref.hpp"
#include "formats/classic_text_reader.hpp"
#include "state/label_catalog.hpp"

namespace openciv::formats::classic {
using MissingLabels = base::CallbackRef<void()>;
// Reads an owned catalog, including home-directory fallback. The reader is
// closed on every exit; no runtime pool, language or cache is changed.
state::LabelCatalog load_labels(TextReader& reader, TextDirectories& directories,
                                const MissingLabels& services = {});
} // namespace openciv::formats::classic
