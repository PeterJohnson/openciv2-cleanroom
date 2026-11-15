export module openciv.state.multiplayer;

import std;
export import openciv.base.random;
export import openciv.state.civilization;
export import openciv.state.preferences;

export namespace openciv::state {
/// F02 session/world ownership and F06 section 4 persistent multiplayer fields.
/// Logical values only; codecs own the 1172-byte file layout. Construction is
/// not the F03 preference default procedure or a classic session reset.
struct MultiplayerValues {
    std::array<std::int16_t, 8> center_x{}, center_y{};
    std::array<bool, 8> introduced{};
    std::int32_t turn_time_limit = 0;
    std::array<std::uint8_t, 256> encoded_passwords{};
    bool open_game = false, kill_on_retirement = false, show_human_starts = false;
    bool chat_from_start = false, simultaneous = false;
    std::int32_t cheat_turn_time = 0;

    std::array<std::string, 8> emails{};
    std::uint8_t resume_civ = 0;
    std::int16_t resume_event = 0, autoplay_turns = 0;
    bool autoplay_active = false;
    std::int16_t claimed_humans = 0;
    bool double_production = false, double_movement = false;
    bool operator==(const MultiplayerValues&) const = default;
};
/// Per-civilization pending/present password indicators.
struct PasswordFlags {
    std::array<bool, 8> pending{}, present{};
    bool operator==(const PasswordFlags&) const = default;
};
/// N03 section 11, shared by option/timer votes. N02 reply dispatch owns updates
/// to pending/status; these are signed counts/results, not membership sets.
struct OptionVoteState {
    std::int32_t pending = 0, status = 0;
    std::uint32_t popup_tick = 0;
    bool operator==(const OptionVoteState&) const = default;
};
/// Retained multiplayer state, separating stored values from runtime authority and votes.
struct MultiplayerState {
    MultiplayerValues saved;
    PasswordFlags passwords;
    // F06 sign-extends saved_current into local_human; the saved resume byte
    // widens unsigned. N01/N04 keep these distinct from the turn/player fields.
    std::int32_t local_human = 0, resume_civ = 0;
    // F02/F06: hotseat roster and network turn selector are distinct from the
    // currently effective human mask and serialized turn/resume fields.
    std::uint8_t hotseat_participants = 0;
    std::int32_t network_turn_civ = 0;
    bool network_host = false; // Runtime authority; not a saved field.
    std::int32_t player_count = 0;
    bool handoff = false;
    OptionVoteState vote{};
    bool operator==(const MultiplayerState&) const = default;
};
/// F06 section 9: rotation spans all 256 bytes, including row and array seams.
void encode_passwords(std::array<std::uint8_t, 256>& bytes) noexcept;
/// Undo classic password encoding across all 256 bytes, including row boundaries.
void decode_passwords(std::array<std::uint8_t, 256>& bytes) noexcept;
/// Refresh password indicators from decoded row prefixes, then restore encoded bytes.
void refresh_password_flags(MultiplayerState& state) noexcept;
/// Exactly 255 primary choices; byte 255 retains its incoming raw value. The
/// owner supplies a unique semantic call identity per reset. A missing choice
/// retains the assigned prefix and leaves the later clearing/encoding undone.
void reset_passwords(MultiplayerState& state, base::GameplayRandom& random, std::string_view call);
/// F06 section 6 completion: parent invokes only after successful input AND
/// close. Equal modes are a no-op. This performs no transport/mode admission.
void adapt_loaded_mode(GameMode entry_mode, GlobalValues& globals, MultiplayerState& state,
                       MultiplayerPreferences& preferences) noexcept;
} // namespace openciv::state

module :private;
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
