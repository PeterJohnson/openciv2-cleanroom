export module openciv.formats.classic_labels;

import std;
export import openciv.base.callback_ref;
export import openciv.formats.classic_text_reader;
export import openciv.state.label_catalog;

export namespace openciv::formats::classic {
using MissingLabels = base::CallbackRef<void()>;
/// Reads an owned catalog, including home-directory fallback. The reader is
/// closed on every exit; no runtime pool, language or cache is changed.
state::LabelCatalog load_labels(TextReader& reader, TextDirectories& directories,
                                const MissingLabels& services = {});
} // namespace openciv::formats::classic

module :private;
namespace openciv::formats::classic {
state::LabelCatalog load_labels(TextReader& reader, TextDirectories& directories,
                                const MissingLabels& services) {
    struct Cleanup {
        TextReader& reader;
        TextDirectories& directories;
        std::string search;
        ~Cleanup() {
            directories.search.swap(search);
            try {
                reader.close();
            } catch (...) {
            }
        }
    } cleanup{reader, directories, directories.search};
    state::LabelCatalog result;
    const auto append = [&](std::int32_t count) {
        if (count > static_cast<std::int32_t>(state::Labels::capacity))
            throw std::out_of_range("Label count exceeds the supported resource table");
        while (static_cast<std::int32_t>(result.entries.size()) < count) {
            reader.next_line();
            result.entries.emplace_back(reader.line());
        }
    };
    if (reader.open("LABELS", "LABELS")) {
        reader.next_line();
        result.declared_count = reader.next_integer();
        append(result.declared_count);
        if (result.entries.size() < state::Labels::capacity) {
            directories.search = directories.home;
            if (reader.open("LABELS", "LABELS")) {
                reader.next_line();
                const auto count = reader.next_integer();
                for (std::int32_t i = 0; i < result.declared_count; ++i)
                    reader.next_line();
                append(count);
            }
        }
        result.entries.resize(state::Labels::capacity, "[ERROR:LABELS.TXT]");
    } else if (services) {
        services();
    }
    reader.close();
    directories.search = cleanup.search;
    if (reader.open("LABELS", "POPUPS"))
        for (auto& popup : result.popups) {
            reader.next_line();
            popup = reader.line();
        }
    reader.close();
    return result;
}
} // namespace openciv::formats::classic
