module openciv.formats.content_pack;

import std;
import openciv.formats.builtin_gameplay_data;

namespace openciv::formats::content {
std::shared_ptr<const GameplayBundle> builtin_gameplay() {
    static const auto bundle = [] {
        const auto manifest = builtin_gameplay_files[0].second;
        auto result = load_gameplay(
            {reinterpret_cast<const Byte*>(manifest.data()), manifest.size()},
            [](std::string_view path) -> Source {
                for (const auto& [name, data] : builtin_gameplay_files)
                    if (name == path)
                        return {"builtin:classic-mge-5.4.0f/1/" + std::string(path),
                                Bytes(data.begin(), data.end())};
                throw Error("source-error", std::string(path), "unknown builtin source");
            },
            true);
        if (result->id != gameplay_target || result->revision != "1")
            throw Error("invalid-content", "builtin gameplay",
                        "registered builtin identity/revision mismatch");
        return result;
    }();
    return bundle;
}
} // namespace openciv::formats::content
