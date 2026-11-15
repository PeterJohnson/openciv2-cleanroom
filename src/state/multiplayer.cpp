#include "state/multiplayer.hpp"

namespace openciv::state {
void encode_passwords(std::array<std::uint8_t, 256>& bytes) noexcept {
    const auto plaintext = bytes;
    for (std::size_t i = 0; i < bytes.size(); ++i)
        bytes[i] = static_cast<std::uint8_t>(
            ((plaintext[i] >> 3) | ((plaintext[(i + 255) % 256] & 7U) << 5)) ^ i);
}
void decode_passwords(std::array<std::uint8_t, 256>& bytes) noexcept {
    const auto encoded = bytes;
    for (std::size_t i = 0; i < bytes.size(); ++i) {
        const auto next = (i + 1) % 256;
        bytes[i] =
            static_cast<std::uint8_t>(((encoded[i] ^ i) << 3) | ((encoded[next] ^ next) >> 5));
    }
}
void refresh_password_flags(MultiplayerState& state) noexcept {
    auto& bytes = state.saved.encoded_passwords;
    decode_passwords(bytes);
    for (std::size_t civ = 0; civ < 8; ++civ) {
        state.passwords.pending[civ] = bytes[civ * 32] != 0;
        state.passwords.present[civ] = bytes[civ * 32] != 0;
    }
    encode_passwords(bytes);
}
void reset_passwords(MultiplayerState& state, base::GameplayRandom& random, std::string_view call) {
    if (call.empty())
        throw std::invalid_argument("Password reset requires a semantic call identity");
    auto& bytes = state.saved.encoded_passwords;
    for (std::size_t i = 0; i < 255; ++i) {
        const base::ChoiceKey key{"F06.password-reset.byte",
                                  std::string(call) + "/row/" + std::to_string(i / 32) + "/byte/" +
                                      std::to_string(i % 32),
                                  0};
        bytes[i] = static_cast<std::uint8_t>(random.primary_code(key));
    }
    for (std::size_t civ = 0; civ < 8; ++civ) {
        bytes[civ * 32] = 0;
        state.passwords.pending[civ] = false;
        state.passwords.present[civ] = false;
    }
    encode_passwords(bytes);
}
void adapt_loaded_mode(GameMode entry_mode, GlobalValues& globals, MultiplayerState& state,
                       MultiplayerPreferences& preferences) noexcept {
    if (entry_mode == globals.multiplayer)
        return;
    state.saved.open_game = true;
    if (entry_mode == state::GameMode::single_player) {
        state.saved.double_production = false;
        state.saved.double_movement = false;
    }
    preferences.simultaneous = false;
    if (globals.human_players == 0) {
        // F01 section 1: variable 32-bit shift counts reduce modulo 32.
        const auto resume_bit = 1U << (static_cast<std::uint32_t>(state.resume_civ) & 31U);
        if (state.resume_civ != 0 && (globals.civs_alive & resume_bit)) {
            globals.human_players = static_cast<std::uint8_t>(resume_bit);
        } else {
            for (unsigned civ = 1; civ < 8; ++civ)
                if (globals.civs_alive & (1U << civ)) {
                    globals.human_players = static_cast<std::uint8_t>(1U << civ);
                    break;
                }
        }
    }
    if (entry_mode != state::GameMode::single_player)
        globals.multiplayer = entry_mode;
}
} // namespace openciv::state
