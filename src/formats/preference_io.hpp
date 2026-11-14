#pragma once

#include "formats/classic_io.hpp"
#include "state/preferences.hpp"

namespace openciv::formats {
inline constexpr std::size_t ordinary_preferences_size = 72, multiplayer_preferences_size = 444;
// F06 CIV2.DAT fixed items. All scalar bit patterns and string tails survive;
// decoding performs no range, termination or session-admission validation.
state::OrdinaryPreferences decode_ordinary_preferences(std::span<const Byte> bytes);
state::MultiplayerPreferences decode_multiplayer_preferences(std::span<const Byte> bytes);
Bytes encode_ordinary_preferences(const state::OrdinaryPreferences& preferences);
Bytes encode_multiplayer_preferences(const state::MultiplayerPreferences& preferences);
// F06/F03: overlay transferred bytes over post-callback live state even when
// the item fails. The caller then chooses its specified fallback defaults.
bool read_ordinary_preferences(const ReadItem& read, state::OrdinaryPreferences& preferences);
bool read_multiplayer_preferences(const ReadItem& read, state::MultiplayerPreferences& preferences);
} // namespace openciv::formats
