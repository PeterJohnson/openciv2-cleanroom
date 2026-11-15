#include "formats/state_conversion.hpp"
// Independently authored F03/F06 native preference comparison driver. Reads
// original data only; never executes or links original game/reconstruction code.
#include "session/classic_preferences.hpp"
#include "formats/preference_io.hpp"
#include "platform/classic_files.hpp"
#include "platform/native_files.hpp"
#include <iostream>

namespace {
// Owns the observed native stream; counters outlive this synchronous read.
class ObservedInput final : public openciv::session::CoreInput {
public:
    ObservedInput(std::unique_ptr<CoreInput> input, bool& complete, std::size_t& items)
        : input_(std::move(input)), complete_(complete), items_(items) {}
    ~ObservedInput() override { close_on_destruction(); }

private:
    openciv::formats::ReadResult read_item(std::size_t size) override {
        auto result = input_->item(size);
        complete_ = complete_ && result.complete;
        ++items_;
        return result;
    }
    std::optional<openciv::formats::Byte> read_byte() override { return input_->byte(); }
    bool finish() override { return input_->close(); }
    std::unique_ptr<CoreInput> input_;
    bool& complete_;
    std::size_t& items_;
};
class ObservedStorage final : public openciv::session::FileStorage {
public:
    ObservedStorage(const openciv::io::ClassicFiles& files, std::string path, bool& complete,
                    std::size_t& items)
        : files_(files), path_(std::move(path)), complete_(complete), items_(items) {}
    std::unique_ptr<openciv::session::CoreInput> read(std::string_view) const override {
        auto input = files_.open_input(path_);
        if (!input)
            throw std::runtime_error("Comparison input could not be reopened");
        return std::make_unique<ObservedInput>(std::move(input), complete_, items_);
    }
    std::unique_ptr<openciv::session::CoreOutput> replace(std::string_view path) const override {
        return files_.open_output(path);
    }
    bool remove(std::string_view path) const override { return files_.remove_file(path); }

private:
    const openciv::io::ClassicFiles& files_;
    std::string path_;
    bool& complete_;
    std::size_t& items_;
};
} // namespace
int main(int argc, char** argv) {
    using namespace openciv;
    try {
        if (argc != 2 && argc != 3)
            throw std::invalid_argument(
                "Usage: oc2_preference_roundtrip INPUT.DAT [NEW_OUTPUT.DAT]");
        const auto bytes = io::read_file(argv[1]);
        constexpr auto size =
            formats::ordinary_preferences_size + formats::multiplayer_preferences_size;
        if (bytes.size() < size)
            throw std::invalid_argument("Comparison requires two complete preference items");
        const auto path = std::filesystem::absolute(argv[1]);
        io::ClassicFiles files(path.parent_path());
        bool complete = true;
        std::size_t items = 0;
        ObservedStorage services(files, io::classic_path(path), complete, items);
        state::ProcessContext process;
        const auto status = session::load_classic_preferences(process, {}, services);
        if (status != session::PreferenceLoadStatus::success || !complete || items != 2)
            throw std::runtime_error(
                "Comparison requires two successful item reads and finalization");
        const auto raw_ordinary = formats::decode_ordinary_preferences(std::span(bytes).first(72));
        const auto raw_multiplayer =
            formats::decode_multiplayer_preferences(std::span(bytes).subspan(72, 444));
        if (process.ordinary_preferences != formats::to_live(raw_ordinary) ||
            process.multiplayer_preferences != formats::to_live(raw_multiplayer))
            throw std::runtime_error("Preference admission differs from decoded values");
        auto encoded = formats::encode_ordinary_preferences(raw_ordinary);
        const auto multiplayer = formats::encode_multiplayer_preferences(raw_multiplayer);
        encoded.insert(encoded.end(), multiplayer.begin(), multiplayer.end());
        if (!std::equal(encoded.begin(), encoded.end(), bytes.begin(), bytes.begin() + size))
            throw std::runtime_error("Loaded and re-encoded preference items differ");
        if (argc == 3)
            io::write_new_file(argv[2], encoded);
        std::cout << "{\"ordinaryBytes\":72,\"multiplayerBytes\":444,\"trailingBytes\":"
                  << bytes.size() - size
                  << ",\"options\":" << process.ordinary_preferences.game_options << "}\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
