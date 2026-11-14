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
    // Snapshot configuration before external I/O; write callbacks are not edits.
    const auto ordinary = formats::encode_ordinary_preferences(process.ordinary_preferences);
    const auto multiplayer =
        formats::encode_multiplayer_preferences(process.multiplayer_preferences);
    auto output = services.open_output("CIV2.DAT");
    if (!output)
        return PreferenceSaveStatus::output;
    bool closed = false;
    const auto close = [&] {
        closed = true;
        return output->close();
    };
    try {
        if (!output->item || !output->close)
            throw std::invalid_argument("Preference output requires write and close services");
        const bool written = output->item(ordinary) && output->item(multiplayer);
        const bool finalized = close();
        if (!written || !finalized) {
            (void)services.remove_file("CIV2.DAT");
            return PreferenceSaveStatus::output;
        }
        return PreferenceSaveStatus::success;
    } catch (...) {
        if (!closed && output->close) {
            try {
                (void)close();
            } catch (...) { /* Preserve the original failure. */
            }
        }
        throw;
    }
}
} // namespace openciv::session
