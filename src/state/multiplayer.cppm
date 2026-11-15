export module openciv.state.multiplayer;

import std;
export import openciv.base.random;
export import openciv.state.civilization;
export import openciv.state.preferences;

export extern "C++" {
    namespace openciv::state {
    // F02 session/world ownership and F06 section 4 persistent multiplayer fields.
    // Logical values only; codecs own the 1172-byte file layout. Construction is
    // not the F03 preference default procedure or a classic session reset.
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
    struct PasswordFlags {
        std::array<bool, 8> pending{}, present{};
        bool operator==(const PasswordFlags&) const = default;
    };
    // N03 section 11, shared by option/timer votes. N02 reply dispatch owns updates
    // to pending/status; these are signed counts/results, not membership sets.
    struct OptionVoteState {
        std::int32_t pending = 0, status = 0;
        std::uint32_t popup_tick = 0;
        bool operator==(const OptionVoteState&) const = default;
    };
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
    // F06 section 9: rotation spans all 256 bytes, including row and array seams.
    void encode_passwords(std::array<std::uint8_t, 256>& bytes) noexcept;
    void decode_passwords(std::array<std::uint8_t, 256>& bytes) noexcept;
    void refresh_password_flags(MultiplayerState& state) noexcept;
    // Exactly 255 primary choices; byte 255 retains its incoming raw value. The
    // owner supplies a unique semantic call identity per reset. A missing choice
    // retains the assigned prefix and leaves the later clearing/encoding undone.
    void reset_passwords(MultiplayerState& state, base::GameplayRandom& random,
                         std::string_view call);
    // F06 section 6 completion: parent invokes only after successful input AND
    // close. Equal modes are a no-op. This performs no transport/mode admission.
    void adapt_loaded_mode(GameMode entry_mode, GlobalValues& globals, MultiplayerState& state,
                           MultiplayerPreferences& preferences) noexcept;
    } // namespace openciv::state
}
