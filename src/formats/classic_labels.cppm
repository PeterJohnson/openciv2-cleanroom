export module openciv.formats.classic_labels;

import std;
export import openciv.base.callback_ref;
export import openciv.formats.classic_text_reader;
export import openciv.state.label_catalog;

export extern "C++" {
    namespace openciv::formats::classic {
    using MissingLabels = base::CallbackRef<void()>;
    // Reads an owned catalog, including home-directory fallback. The reader is
    // closed on every exit; no runtime pool, language or cache is changed.
    state::LabelCatalog load_labels(TextReader& reader, TextDirectories& directories,
                                    const MissingLabels& services = {});
    } // namespace openciv::formats::classic
}
