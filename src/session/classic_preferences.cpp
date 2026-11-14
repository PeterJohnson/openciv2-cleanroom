#include "session/classic_preferences.hpp"
#include "formats/preference_io.hpp"

namespace openciv::session {
void load_classic_preferences(state::ProcessContext& process,
                              const state::PreferenceDefaults& defaults,
                              const PreferenceFileServices& services) {
    auto input = services.open_input("CIV2.DAT");
    if (!input) {
        state::default_ordinary_preferences(process.ordinary_preferences, defaults);
        state::default_multiplayer_preferences(process.multiplayer_preferences);
        return;
    }
    bool closed = false;
    const auto close = [&] {
        if (!closed) {
            closed = true;
            (void)input->close();
        }
    };
    try {
        if (!formats::read_ordinary_preferences(input->item, process.ordinary_preferences))
            state::default_ordinary_preferences(process.ordinary_preferences, defaults);
        if (!formats::read_multiplayer_preferences(input->item, process.multiplayer_preferences))
            state::default_multiplayer_preferences(process.multiplayer_preferences);
        close();
    } catch (...) {
        close();
        throw;
    }
}
PreferenceSaveStatus save_classic_preferences(const state::ProcessContext& process,
                                              const PreferenceFileServices& services) {
    auto output = services.open_output("CIV2.DAT");
    if (!output)
        return PreferenceSaveStatus::output;
    bool closed = false;
    const auto close = [&] {
        if (!closed) {
            closed = true;
            (void)output->close();
        }
    };
    try {
        const bool success =
            output->item(formats::encode_ordinary_preferences(process.ordinary_preferences)) &&
            output->item(formats::encode_multiplayer_preferences(process.multiplayer_preferences));
        close();
        if (!success)
            (void)services.remove_file("CIV2.DAT");
        return success ? PreferenceSaveStatus::success : PreferenceSaveStatus::output;
    } catch (...) {
        close();
        throw;
    }
}
} // namespace openciv::session
