#include "formats/preference_io.hpp"

namespace openciv::formats {
namespace {
ByteReader record(std::span<const Byte> bytes, std::size_t size) {
    if (bytes.size() != size)
        throw DecodeError(std::min(size, bytes.size()), "Incorrect preference item length");
    return ByteReader{bytes};
}
template <class Range> void read_bytes(ByteReader& reader, Range& destination) {
    for (auto& byte : destination)
        byte = reader.u8();
}
state::Rectangle rectangle(ByteReader& reader) {
    return {reader.i32(), reader.i32(), reader.i32(), reader.i32()};
}
void rectangle(ByteWriter& writer, const state::Rectangle& value) {
    writer.i32(value.left);
    writer.i32(value.top);
    writer.i32(value.right);
    writer.i32(value.bottom);
}
template <class Record, class Encode, class Decode>
Record decode_item(const ReadResult& incoming, std::size_t size, const Record& prior, Encode encode,
                   Decode decode) {
    if (incoming.prefix.size() > size || (incoming.complete && incoming.prefix.size() != size))
        throw std::invalid_argument("Preference read returned an inconsistent transferred prefix");
    auto bytes = encode(prior);
    std::copy(incoming.prefix.begin(), incoming.prefix.end(), bytes.begin());
    return decode(bytes);
}
} // namespace
state::OrdinaryPreferences decode_ordinary_preferences(std::span<const Byte> bytes) {
    auto r = record(bytes, ordinary_preferences_size);
    state::OrdinaryPreferences p;
    p.rules = r.u16();
    p.default_level = r.i16();
    p.difficulty = r.i16();
    p.tribe = r.i16();
    p.gender = r.i16();
    p.science = r.i16();
    p.tax = r.i16();
    p.game_options = r.u32();
    p.disabled_messages = r.u16();
    p.opponents = r.i16();
    p.map_size = r.i16();
    p.barbarians = r.i16();
    for (auto& value : p.world_parameters)
        value = r.i16();
    read_bytes(r, p.text.bytes);
    p.rules_choice = r.i16();
    p.accelerated_start = r.i16();
    return p;
}
Bytes encode_ordinary_preferences(const state::OrdinaryPreferences& p) {
    ByteWriter w;
    w.u16(p.rules);
    w.i16(p.default_level);
    w.i16(p.difficulty);
    w.i16(p.tribe);
    w.i16(p.gender);
    w.i16(p.science);
    w.i16(p.tax);
    w.u32(p.game_options);
    w.u16(p.disabled_messages);
    w.i16(p.opponents);
    w.i16(p.map_size);
    w.i16(p.barbarians);
    for (const auto value : p.world_parameters)
        w.i16(value);
    w.append(p.text.bytes);
    w.i16(p.rules_choice);
    w.i16(p.accelerated_start);
    return std::move(w.bytes);
}
state::MultiplayerPreferences decode_multiplayer_preferences(std::span<const Byte> bytes) {
    auto r = record(bytes, multiplayer_preferences_size);
    state::MultiplayerPreferences p;
    p.main_menu = r.i16();
    p.type = r.i16();
    p.hotseat_menu = r.i16();
    p.lan_menu = r.i16();
    p.legacy_setting_1 = r.i16();
    p.email_menu = r.i16();
    p.protocol = r.u16();
    p.open_game = r.i16();
    p.kill_on_retirement = r.i16();
    p.show_human_starts = r.i16();
    p.chat_from_start = r.i16();
    p.simultaneous = r.i16();
    for (auto* value : {&p.game_name, &p.player_name, &p.email_address, &p.ip_address})
        read_bytes(r, value->bytes);
    p.timer = r.i16();
    read_bytes(r, p.custom_timer.bytes);
    p.hotseat_players = r.i16();
    p.parley = rectangle(r);
    p.chat = rectangle(r);
    p.clear_chat_new_game = r.i16();
    p.clear_chat_boot = r.i16();
    p.double_production = r.i16();
    p.double_movement = r.i16();
    for (auto& setting : p.legacy_settings)
        read_bytes(r, setting);
    for (auto& setting : p.settings)
        read_bytes(r, setting);
    p.legacy_setting_5 = r.i16();
    return p;
}
Bytes encode_multiplayer_preferences(const state::MultiplayerPreferences& p) {
    ByteWriter w;
    w.i16(p.main_menu);
    w.i16(p.type);
    w.i16(p.hotseat_menu);
    w.i16(p.lan_menu);
    w.i16(p.legacy_setting_1);
    w.i16(p.email_menu);
    w.u16(p.protocol);
    w.i16(p.open_game);
    w.i16(p.kill_on_retirement);
    w.i16(p.show_human_starts);
    w.i16(p.chat_from_start);
    w.i16(p.simultaneous);
    for (const auto* value : {&p.game_name, &p.player_name, &p.email_address, &p.ip_address})
        w.append(value->bytes);
    w.i16(p.timer);
    w.append(p.custom_timer.bytes);
    w.i16(p.hotseat_players);
    rectangle(w, p.parley);
    rectangle(w, p.chat);
    w.i16(p.clear_chat_new_game);
    w.i16(p.clear_chat_boot);
    w.i16(p.double_production);
    w.i16(p.double_movement);
    for (const auto& setting : p.legacy_settings)
        w.append(setting);
    for (const auto& setting : p.settings)
        w.append(setting);
    w.i16(p.legacy_setting_5);
    return std::move(w.bytes);
}
state::OrdinaryPreferences
decode_ordinary_preference_item(const ReadResult& item, const state::OrdinaryPreferences& prior) {
    return decode_item(item, ordinary_preferences_size, prior, encode_ordinary_preferences,
                       decode_ordinary_preferences);
}
state::MultiplayerPreferences
decode_multiplayer_preference_item(const ReadResult& item,
                                   const state::MultiplayerPreferences& prior) {
    return decode_item(item, multiplayer_preferences_size, prior, encode_multiplayer_preferences,
                       decode_multiplayer_preferences);
}
} // namespace openciv::formats
