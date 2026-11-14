// Independently authored F03/F06 native preference comparison driver. Reads
// original data only; never executes or links original game/reconstruction code.
#include "session/classic_preferences.hpp"
#include "formats/preference_io.hpp"
#include "platform/classic_files.hpp"
#include "platform/native_files.hpp"
#include <iostream>

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
        session::PreferenceFileServices services;
        bool complete = true;
        std::size_t items = 0;
        services.open_input = [&](std::string_view) {
            auto input = files.open_input(io::classic_path(path));
            if (!input)
                throw std::runtime_error("Comparison input could not be reopened");
            const auto read = input->item;
            input->item = [&, read](std::size_t size) {
                auto item = read(size);
                complete = complete && item.complete;
                ++items;
                return item;
            };
            return input;
        };
        state::ProcessContext process;
        session::load_classic_preferences(process, {}, services);
        if (!complete || items != 2)
            throw std::runtime_error("Comparison requires two successful item reads");
        auto encoded = formats::encode_ordinary_preferences(process.ordinary_preferences);
        const auto multiplayer =
            formats::encode_multiplayer_preferences(process.multiplayer_preferences);
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
