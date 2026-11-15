export module openciv.formats.preference_io;

import std;
export import openciv.state.preferences;
export import openciv.formats.classic_io;
export import openciv.formats.stored_records;

export namespace openciv::formats {
inline constexpr std::size_t ordinary_preferences_size = 72, multiplayer_preferences_size = 444;
/// F06 CIV2.DAT fixed items. All scalar bit patterns and string tails survive;
/// decoding performs no range, termination or session-admission validation.
StoredOrdinaryPreferences decode_ordinary_preferences(std::span<const Byte> bytes);
/// Decode the stored multiplayer preference record without installing it.
StoredMultiplayerPreferences decode_multiplayer_preferences(std::span<const Byte> bytes);
/// Serialize retained ordinary preferences in their classic byte layout.
Bytes encode_ordinary_preferences(const StoredOrdinaryPreferences& preferences);
/// Serialize retained multiplayer preferences in their classic byte layout.
Bytes encode_multiplayer_preferences(const StoredMultiplayerPreferences& preferences);
/// Expand an owned transferred item against an explicit prior snapshot. A short
/// item still retains its transferred prefix; the session chooses fallback defaults.
StoredOrdinaryPreferences decode_ordinary_preference_item(const io::ReadResult& item,
                                                          const StoredOrdinaryPreferences& prior);
/// Apply the multiplayer preference item-read contract to the prior stored record.
StoredMultiplayerPreferences
decode_multiplayer_preference_item(const io::ReadResult& item,
                                   const StoredMultiplayerPreferences& prior);
/// Encode live ordinary preferences through the classic storage conversion.
Bytes encode_ordinary_preferences(const state::OrdinaryPreferences& value);
/// Encode live multiplayer preferences through the classic storage conversion.
Bytes encode_multiplayer_preferences(const state::MultiplayerPreferences& value);
} // namespace openciv::formats
