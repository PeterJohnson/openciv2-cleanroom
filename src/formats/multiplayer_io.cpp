#include "formats/multiplayer_io.hpp"
#include <algorithm>

namespace openciv::formats {
bool read_multiplayer_items(const ReadItem& read, std::uint16_t version,
                            const state::GlobalValues& globals, state::MultiplayerState& state,
                            state::MultiplayerPreferences& preferences,
                            base::GameplayRandom& random, std::string_view reset_call) {
    if (game_version_status(version) != VersionStatus::supported)
        throw std::invalid_argument("Unsupported core multiplayer record version");
    state.local_human = globals.saved_current_civ;
    auto& saved = state.saved;
    if (version < 0x29 || globals.multiplayer == 0) {
        state::reset_passwords(state, random, reset_call);
        saved.open_game = 1;
        saved.kill_on_retirement = preferences.kill_on_retirement;
        saved.show_human_starts = preferences.show_human_starts;
        saved.chat_from_start = preferences.chat_from_start;
        saved.double_production = 0;
        saved.double_movement = 0;
        preferences.simultaneous = 0;
        saved.resume_civ = static_cast<std::uint8_t>(state.local_human);
        state.resume_civ = saved.resume_civ;
        return true;
    }
    saved = {};
    const auto result = read(multiplayer_record_size);
    if (result.prefix.size() > multiplayer_record_size ||
        (result.complete && result.prefix.size() != multiplayer_record_size))
        throw std::invalid_argument("Item service returned an inconsistent transferred prefix");
    auto bytes = encode_multiplayer(saved);
    std::copy(result.prefix.begin(), result.prefix.end(), bytes.begin());
    saved = decode_multiplayer(bytes);
    if (!result.complete)
        return false;
    if (saved.resume_civ == 0) {
        saved.resume_civ = static_cast<std::uint8_t>(globals.turn_civ);
        if (saved.resume_civ == 0)
            for (unsigned civ = 1; civ < 8; ++civ)
                if (globals.civs_alive & (1U << civ)) {
                    saved.resume_civ = static_cast<std::uint8_t>(civ);
                    break;
                }
    }
    state.resume_civ = saved.resume_civ;
    state::refresh_password_flags(state);
    return true;
}
bool write_multiplayer_item(const WriteItem& write, const state::MultiplayerState& state,
                            std::int32_t network_turn_civ) {
    auto saved = state.saved;
    saved.resume_civ = static_cast<std::uint8_t>(network_turn_civ);
    return write(encode_multiplayer(saved));
}
} // namespace openciv::formats
