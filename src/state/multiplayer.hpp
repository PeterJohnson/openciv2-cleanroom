#pragma once

#include "base/random.hpp"
#include "state/civilization.hpp"
#include "state/preferences.hpp"

namespace openciv::state {
// F02 session/world ownership and F06 section 4 persistent multiplayer fields.
// Logical values only; codecs own the 1172-byte file layout. Construction is
// not the F03 preference default procedure or a classic session reset.
struct MultiplayerValues {
    std::array<std::int16_t, 8> center_x{}, center_y{}, introduced{};
    std::int32_t turn_time_limit = 0;
    std::array<std::uint8_t, 256> encoded_passwords{};
    std::int16_t open_game = 0, kill_on_retirement = 0, show_human_starts = 0;
    std::int16_t chat_from_start = 0, simultaneous = 0;
    std::int32_t cheat_turn_time = 0;
    std::array<std::uint8_t, 290> retained_142{};
    std::array<std::array<std::uint8_t, 32>, 8> emails{}, player_setup{};
    std::uint8_t resume_civ = 0, retained_465 = 0;
    std::int16_t resume_event = 0, autoplay_active = 0, autoplay_turns = 0;
    std::int16_t double_production = 0, double_movement = 0, claimed_humans = 0;
    std::array<std::uint8_t, 34> retained_472{};
    bool operator==(const MultiplayerValues&) const = default;
};
struct PasswordFlags {
    std::array<bool, 8> pending{}, present{};
    bool operator==(const PasswordFlags&) const = default;
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
    bool operator==(const MultiplayerState&) const = default;
};
// F06 section 9: rotation spans all 256 bytes, including row and array seams.
void encode_passwords(std::array<std::uint8_t, 256>& bytes) noexcept;
void decode_passwords(std::array<std::uint8_t, 256>& bytes) noexcept;
void refresh_password_flags(MultiplayerState& state) noexcept;
// Exactly 255 primary choices; byte 255 retains its incoming raw value. The
// owner supplies a unique semantic call identity per reset. A missing choice
// retains the assigned prefix and leaves the later clearing/encoding undone.
void reset_passwords(MultiplayerState& state, base::GameplayRandom& random, std::string_view call);
// F06 section 6 completion: parent invokes only after successful input AND
// close. Equal modes are a no-op. This performs no transport/mode admission.
void adapt_loaded_mode(std::uint8_t entry_mode, GlobalValues& globals, MultiplayerState& state,
                       MultiplayerPreferences& preferences) noexcept;
} // namespace openciv::state
