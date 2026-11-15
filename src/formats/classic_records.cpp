module openciv.formats.classic_records;

import std;
import openciv.formats.state_conversion;

namespace openciv::formats {
namespace {
ByteReader record(std::span<const Byte> bytes, std::size_t size) {
    if (bytes.size() != size)
        throw DecodeError(std::min(size, bytes.size()), "Incorrect fixed-record length");
    return ByteReader{bytes};
}
template <class Range> void read_bytes(ByteReader& reader, Range& target) {
    for (auto& byte : target)
        byte = reader.u8();
}
} // namespace
StoredUnit decode_unit(std::span<const Byte> bytes) {
    auto r = record(bytes, unit_record_size);
    StoredUnit v;
    v.x = r.i16();
    v.y = r.i16();
    v.flags = r.u16();
    v.type = state::UnitType{r.u8()};
    v.owner = state::CivilizationSlot{r.u8()};
    v.moves_used = r.u8();
    v.visibility = r.u8();
    v.damage = r.u8();
    v.last_direction = r.u8();
    v.ai_task = r.u8();
    v.counter = r.u8();
    v.auxiliary_timer = r.u8();
    v.orders = static_cast<state::UnitOrder>(r.i8());
    const auto home = r.u8();
    if (home != 255)
        v.home = state::CitySlot{home};
    v.retained_byte = r.u8();
    v.destination_x = r.i16();
    v.destination_y = r.i16();
    v.previous.value = r.i16();
    v.next.value = r.i16();
    v.token = state::UnitToken{r.u32()};
    v.retained_word = r.u16();
    return v;
}
Bytes encode_unit(const StoredUnit& v) {
    if (v.home && static_cast<std::uint16_t>(*v.home) >= 255)
        throw std::invalid_argument("Unit home city outside classic encoding");
    ByteWriter w;
    w.i16(v.x);
    w.i16(v.y);
    w.u16(v.flags);
    w.u8(static_cast<Byte>(v.type));
    w.u8(static_cast<Byte>(v.owner));
    w.u8(v.moves_used);
    w.u8(v.visibility);
    w.u8(v.damage);
    w.u8(v.last_direction);
    w.u8(v.ai_task);
    w.u8(v.counter);
    w.u8(v.auxiliary_timer);
    w.i8(static_cast<std::int8_t>(v.orders));
    w.u8(v.home ? static_cast<Byte>(*v.home) : Byte{255});
    w.u8(v.retained_byte);
    w.i16(v.destination_x);
    w.i16(v.destination_y);
    w.i16(v.previous.value);
    w.i16(v.next.value);
    w.u32(static_cast<std::uint32_t>(v.token));
    w.u16(v.retained_word);
    return std::move(w.bytes);
}
StoredCity decode_city(std::span<const Byte> bytes) {
    auto r = record(bytes, city_record_size);
    StoredCity v;
    v.x = r.i16();
    v.y = r.i16();
    v.flags = r.u32();
    v.owner = state::CivilizationSlot{r.u8()};
    v.size = r.i8();
    v.founder = state::CivilizationSlot{r.u8()};
    v.turns_captured = r.u8();
    v.known_to = r.u8();
    read_bytes(r, v.remembered_size);
    v.retained_byte = r.u8();
    v.specialist_kinds = r.u32();
    v.food_box = r.i16();
    v.shield_box = r.i16();
    v.base_trade = r.i16();
    read_bytes(r, v.name.bytes);
    v.worked_tiles = r.u32();
    read_bytes(r, v.improvements);
    v.production = r.i8();
    v.route_count = r.u8();
    for (auto& value : v.supplies)
        value = r.i8();
    for (auto& value : v.demands)
        value = r.i8();
    for (auto& value : v.route_goods)
        value = r.i8();
    for (auto& value : v.route_partners)
        value.value = r.i16();
    v.science = r.i16();
    v.tax = r.i16();
    v.trade = r.i16();
    v.total_food = r.u8();
    v.total_shields = r.u8();
    v.happy = r.u8();
    v.unhappy = r.u8();
    v.token = state::CityToken{r.u32()};
    return v;
}
Bytes encode_city(const StoredCity& v) {
    ByteWriter w;
    w.i16(v.x);
    w.i16(v.y);
    w.u32(v.flags);
    w.u8(static_cast<Byte>(v.owner));
    w.i8(v.size);
    w.u8(static_cast<Byte>(v.founder));
    w.u8(v.turns_captured);
    w.u8(v.known_to);
    w.append(v.remembered_size);
    w.u8(v.retained_byte);
    w.u32(v.specialist_kinds);
    w.i16(v.food_box);
    w.i16(v.shield_box);
    w.i16(v.base_trade);
    w.append(v.name.bytes);
    w.u32(v.worked_tiles);
    w.append(v.improvements);
    w.i8(v.production);
    w.u8(v.route_count);
    for (auto value : v.supplies)
        w.i8(value);
    for (auto value : v.demands)
        w.i8(value);
    for (auto value : v.route_goods)
        w.i8(value);
    for (auto value : v.route_partners)
        w.i16(value.value);
    w.i16(v.science);
    w.i16(v.tax);
    w.i16(v.trade);
    w.u8(v.total_food);
    w.u8(v.total_shields);
    w.u8(v.happy);
    w.u8(v.unhappy);
    w.u32(static_cast<std::uint32_t>(v.token));
    return std::move(w.bytes);
}
world::Tile decode_tile(std::span<const Byte> bytes) {
    auto r = record(bytes, tile_record_size);
    world::Tile v;
    const auto terrain = r.u8();
    v.terrain = static_cast<world::Terrain>(terrain & 15);
    v.terrain_flags = terrain & 0xf0;
    v.features = r.u8();
    const auto radius = r.u8();
    v.claimant = radius >> 5;
    v.radius_other = radius & 31;
    v.region = r.u8();
    v.revelation = r.u8();
    const auto site = r.u8();
    v.fertility = site & 15;
    v.owner = (site >> 4) == 15 ? std::nullopt : std::optional{static_cast<Byte>(site >> 4)};
    return v;
}
Bytes encode_tile(const world::Tile& v) {
    if (static_cast<Byte>(v.terrain) > 15 || (v.terrain_flags & 15) != 0 || v.claimant > 7 ||
        v.radius_other > 31 || v.fertility > 15 || (v.owner && *v.owner > 14))
        throw std::invalid_argument("Tile field outside classic encoding");
    return {static_cast<Byte>(static_cast<Byte>(v.terrain) | v.terrain_flags),
            v.features,
            static_cast<Byte>((v.claimant << 5) | v.radius_other),
            v.region,
            v.revelation,
            static_cast<Byte>((v.owner.value_or(15) << 4) | v.fertility)};
}
world::RegionSummary decode_region(std::span<const Byte> bytes) {
    auto r = record(bytes, region_record_size);
    world::RegionSummary v;
    v.land = r.i16();
    v.fertile = r.i16();
    v.water = r.i16();
    v.retained[0] = r.u8();
    v.neighboring_water = r.u64();
    v.retained[1] = r.u8();
    return v;
}
Bytes encode_region(const world::RegionSummary& v) {
    ByteWriter w;
    w.i16(v.land);
    w.i16(v.fertile);
    w.i16(v.water);
    w.u8(v.retained[0]);
    w.u64(v.neighboring_water);
    w.u8(v.retained[1]);
    return std::move(w.bytes);
}
MapHeader decode_map_header(std::span<const Byte> bytes) {
    auto r = record(bytes, map_header_size);
    MapHeader v;
    v.width = r.i16();
    v.height = r.i16();
    v.area = r.i16();
    v.topology = r.u16();
    v.seed = r.u16();
    v.coarse_width = r.i16();
    v.coarse_height = r.i16();
    return v;
}
Bytes encode_map_header(const MapHeader& v) {
    ByteWriter w;
    w.i16(v.width);
    w.i16(v.height);
    w.i16(v.area);
    w.u16(v.topology);
    w.u16(v.seed);
    w.i16(v.coarse_width);
    w.i16(v.coarse_height);
    return std::move(w.bytes);
}
Bytes encode_unit(const state::Unit& value) {
    return encode_unit(to_stored(value));
}
Bytes encode_city(const state::City& value) {
    return encode_city(to_stored(value));
}
} // namespace openciv::formats
