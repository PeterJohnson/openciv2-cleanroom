export module openciv.session.classic_premade_world;

import std;
export import openciv.session.premade_world;
export import openciv.io.storage;
import openciv.formats.game_input;
export namespace openciv::session {
class ClassicPremadeWorldSource final : public PremadeWorldSource {
public:
    explicit ClassicPremadeWorldSource(const io::FileStorage& storage) : storage_(storage) {}
    std::optional<PremadeWorldInput> load(std::string_view path) const override;

private:
    const io::FileStorage& storage_;
};
} // namespace openciv::session

module :private;
namespace openciv::session {
std::optional<PremadeWorldInput> ClassicPremadeWorldSource::load(std::string_view path) const {
    auto input = storage_.read(path);
    if (!input)
        return {};
    formats::Bytes bytes;
    for (;;) {
        auto part = input->item(65536);
        if (part.prefix.size() > 65536 || (part.complete && part.prefix.size() != 65536))
            throw std::invalid_argument("Inconsistent map transfer");
        if (bytes.size() + part.prefix.size() > 256 * 1024 * 1024)
            throw std::length_error("Map exceeds admission limit");
        bytes.insert(bytes.end(), part.prefix.begin(), part.prefix.end());
        if (!part.complete)
            break;
    }
    const bool closed = input->close();
    std::optional<formats::RawMapFile> decoded;
    try {
        decoded = formats::decode_raw_map(bytes);
    } catch (const formats::DecodeError&) {
    }
    if (!closed || !decoded)
        return {};
    const auto starts = decoded->starts;
    return PremadeWorldInput{formats::map_input(std::move(*decoded)), starts};
}
} // namespace openciv::session
