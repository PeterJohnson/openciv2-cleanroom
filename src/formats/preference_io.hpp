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
// Expand an owned transferred item against an explicit prior snapshot. A short
// item still retains its transferred prefix; the session chooses fallback defaults.
state::OrdinaryPreferences decode_ordinary_preference_item(const ReadResult& item,
                                                           const state::OrdinaryPreferences& prior);
state::MultiplayerPreferences
decode_multiplayer_preference_item(const ReadResult& item,
                                   const state::MultiplayerPreferences& prior);
} // namespace openciv::formats
