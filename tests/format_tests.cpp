#include "formats/classic_io.hpp"
#include <catch2/catch_test_macros.hpp>
#include <limits>
#include <numeric>

using namespace openciv;
using namespace openciv::formats;

namespace {
// Independently authored F06 §3 bytes. No host structs or codec produce these
// expectations. They deliberately include signed boundaries and retained bits.
const Bytes unit_vector{0x38, 0xff, 0x38, 0xff, 0x01, 0x80, 0x03, 0x07, 0xff, 0x81, 0x0a,
                        0xff, 0x02, 0x80, 0xfe, 0x03, 0xc8, 0xa5, 0xff, 0x07, 0x00, 0x80,
                        0xfe, 0xff, 0x00, 0x00, 0x01, 0x00, 0x00, 0x80, 0xef, 0xbe};
Bytes raw_map_vector() {
    // W=4,H=2,A=4, topology=0x1234, seed=24, CW=CH=1.
    Bytes bytes{4, 0, 2, 0, 4, 0, 0x34, 0x12, 24, 0, 1, 0, 1, 0};
    // X first, then Y; tribe 0=(2,1), remaining starts absent.
    bytes.insert(bytes.end(), {2, 0});
    bytes.insert(bytes.end(), 40, 0xff);
    bytes.insert(bytes.end(), {1, 0});
    bytes.insert(bytes.end(), 40, 0xff);
    const Bytes tiles{0xc0, 0x42, 0xe5, 0xff, 0x81, 0xff, 0x0a, 0x00, 0x00, 0x3f, 0x00, 0x00,
                      0x02, 0x20, 0x01, 0x02, 0xff, 0x8f, 0x8b, 0x80, 0x1f, 0x03, 0x01, 0xe9};
    bytes.insert(bytes.end(), tiles.begin(), tiles.end());
    return bytes;
}
} // namespace

TEST_CASE("F06 unit records have exact explicit encodings and unsigned home slots",
          "[formats][F06]") {
    const auto before = unit_vector;
    const auto unit = decode_unit(unit_vector);
    CHECK(unit.x == -200);
    CHECK(unit.y == -200);
    CHECK(unit.flags == 0x8001);
    CHECK(unit.type == state::UnitType{3});
    CHECK(unit.owner == state::CivilizationSlot{7});
    CHECK(unit.moves_used == 255);
    CHECK(unit.visibility == 0x81);
    CHECK(unit.damage == 10);
    CHECK(unit.last_direction == 255);
    CHECK(unit.ai_task == 2);
    CHECK(unit.counter == 128);
    CHECK(unit.auxiliary_timer == 254);
    CHECK(unit.orders == state::UnitOrder::sleeping);
    CHECK(unit.home == state::CitySlot{200});
    CHECK(unit.retained_byte == 0xa5);
    CHECK(unit.destination_x == 2047);
    CHECK(unit.destination_y == -32768);
    CHECK(unit.previous.value == -2);
    CHECK_FALSE(unit.previous.slot());
    CHECK(unit.next.slot() == state::UnitSlot{0});
    CHECK(unit.token == state::UnitToken{0x80000001});
    CHECK(state::occupied(unit.token));
    CHECK(unit.retained_word == 0xbeef);
    CHECK(encode_unit(unit) == unit_vector);
    CHECK(unit_vector == before);
    for (unsigned home = 0; home < 256; ++home) {
        auto bytes = unit_vector;
        bytes[16] = static_cast<Byte>(home);
        const auto decoded = decode_unit(bytes);
        CHECK(decoded.home ==
              (home == 255 ? std::nullopt
                           : std::optional{state::CitySlot{static_cast<std::uint16_t>(home)}}));
        CHECK(encode_unit(decoded) == bytes);
    }
    auto bad = unit;
    bad.home = state::CitySlot{255};
    CHECK_THROWS_AS(encode_unit(bad), std::invalid_argument);
}

TEST_CASE("F06 city records retain text tails unused bits signed stocks and route order",
          "[formats][F06]") {
    Bytes bytes(88);
    std::iota(bytes.begin(), bytes.end(), Byte{0});
    bytes[9] = 0x80;
    bytes[0x1a] = 0;
    bytes[0x1b] = 0x80;
    bytes[0x1c] = 0xff;
    bytes[0x1d] = 0x7f;
    bytes[0x20] = 'X';
    bytes[0x21] = 0;
    bytes[0x39] = 0xd9;
    bytes[0x3b] = 0;
    bytes[0x3c] = 0xff;
    bytes[0x3d] = 0x80;
    bytes[0x41] = 0xff;
    bytes[0x54] = 0xff;
    bytes[0x55] = 0xff;
    bytes[0x56] = 0xff;
    bytes[0x57] = 0xff;
    const auto city = decode_city(bytes);
    CHECK(city.x == 0x0100);
    CHECK(city.y == 0x0302);
    CHECK(city.flags == 0x07060504);
    CHECK(city.owner == state::CivilizationSlot{8});
    CHECK(city.size == -128);
    CHECK(city.founder == state::CivilizationSlot{10});
    CHECK(city.turns_captured == 11);
    CHECK(city.known_to == 12);
    CHECK(city.remembered_size[0] == 13);
    CHECK(city.remembered_size[7] == 20);
    CHECK(city.retained_byte == 21);
    CHECK(city.specialist_kinds == 0x19181716);
    CHECK(city.food_box == -32768);
    CHECK(city.shield_box == 32767);
    CHECK(city.base_trade == 0x1f1e);
    CHECK(city.name.terminated());
    CHECK(city.name.text().size() == 1);
    CHECK(city.name.text()[0] == 'X');
    CHECK(city.name.bytes[2] == 0x22);
    CHECK(city.worked_tiles == 0x33323130);
    CHECK(city.improvements[4] == 0x38);
    CHECK(city.production == -39);
    CHECK(city.route_count == 0x3a);
    CHECK(city.supplies == std::array<std::int8_t, 3>{0, -1, -128});
    CHECK(city.demands == std::array<std::int8_t, 3>{0x3e, 0x3f, 0x40});
    CHECK(city.route_goods[0] == -1);
    CHECK(city.route_partners[0].value == 0x4544);
    CHECK(city.route_partners[2].value == 0x4948);
    CHECK(city.science == 0x4b4a);
    CHECK(city.tax == 0x4d4c);
    CHECK(city.trade == 0x4f4e);
    CHECK(city.total_food == 0x50);
    CHECK(city.total_shields == 0x51);
    CHECK(city.happy == 0x52);
    CHECK(city.unhappy == 0x53);
    CHECK(city.token == state::CityToken{0xffffffff});
    CHECK(encode_city(city) == bytes);
    auto unterminated = city;
    unterminated.name.bytes.fill(0xe9);
    CHECK_FALSE(unterminated.name.terminated());
    CHECK(unterminated.name.text().size() == 16); // decoded record is not text normalization
}

TEST_CASE("F06 tile and region codecs preserve all stored bit domains", "[formats][F06]") {
    const Bytes bytes{0xcb, 0x42, 0xe5, 0xff, 0x81, 0xef};
    const auto tile = decode_tile(bytes);
    CHECK(static_cast<Byte>(tile.terrain) == 11);
    CHECK(tile.terrain_flags == 0xc0);
    CHECK(tile.airbase());
    CHECK(tile.claimant == 7);
    CHECK(tile.radius_other == 5);
    CHECK(tile.region == 255);
    CHECK(tile.revelation == 0x81);
    CHECK(tile.owner == 14);
    CHECK(tile.fertility == 15);
    CHECK(encode_tile(tile) == bytes);
    for (std::size_t offset = 0; offset < 6; ++offset)
        for (unsigned value = 0; value < 256; ++value) {
            auto changed = bytes;
            changed[offset] = static_cast<Byte>(value);
            CHECK(encode_tile(decode_tile(changed)) == changed);
        }
    const Bytes region{0x00, 0x80, 0xff, 0x7f, 0xff, 0xff, 0xa5, 0x01,
                       0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x5a};
    const auto summary = decode_region(region);
    CHECK(summary.land == -32768);
    CHECK(summary.fertile == 32767);
    CHECK(summary.water == -1);
    CHECK(summary.retained == std::array<Byte, 2>{0xa5, 0x5a});
    CHECK(summary.neighboring_water == 0x8040201008040201ULL);
    CHECK(encode_region(summary) == region);
}

TEST_CASE("F06 fixed codecs reject truncation and extra record bytes", "[formats][F06]") {
    for (auto size : {unit_record_size, city_record_size, tile_record_size, region_record_size,
                      map_header_size}) {
        for (std::size_t length = 0; length <= size + 1; ++length) {
            if (length == size)
                continue;
            const Bytes bytes(length);
            if (size == unit_record_size)
                CHECK_THROWS_AS(decode_unit(bytes), DecodeError);
            if (size == city_record_size)
                CHECK_THROWS_AS(decode_city(bytes), DecodeError);
            if (size == tile_record_size)
                CHECK_THROWS_AS(decode_tile(bytes), DecodeError);
            if (size == region_record_size)
                CHECK_THROWS_AS(decode_region(bytes), DecodeError);
            if (size == map_header_size)
                CHECK_THROWS_AS(decode_map_header(bytes), DecodeError);
        }
    }
    ByteReader reader{unit_vector};
    CHECK_THROWS_AS(reader.take(std::numeric_limits<std::size_t>::max()), DecodeError);
    CHECK(reader.position() == 0);
}

TEST_CASE("F06 current entity load renews only after both blocks and retains failure prefixes",
          "[formats][F06]") {
    state::EntityPools source;
    source.units.set_end(3);
    source.cities.set_end(2);
    source.units.storage()[0] = decode_unit(unit_vector);
    source.units.storage()[0].token = state::UnitToken{50};
    source.units.storage()[1].damage = 99; // hole still has stored payload
    source.units.storage()[2].token = state::UnitToken{90};
    source.cities.storage()[0].token = state::CityToken{7};
    source.cities.storage()[0].size = -128;
    source.cities.storage()[1].token = state::CityToken{9};
    source.cities.storage()[1].name.bytes[0] = 'B';
    Bytes file;
    std::vector<std::size_t> sizes;
    REQUIRE(write_entity_items(
        [&](auto bytes) {
            sizes.push_back(bytes.size());
            file.insert(file.end(), bytes.begin(), bytes.end());
            return true;
        },
        source));
    CHECK(sizes == std::vector<std::size_t>{96, 176});
    const auto untouched = file;
    for (const std::size_t available : {std::size_t{0}, std::size_t{31}, std::size_t{96},
                                        std::size_t{96 + 88 + 33}, file.size()}) {
        state::EntityPools destination;
        destination.units.set_end(3);
        destination.cities.set_end(2);
        destination.cities.storage()[1].name.bytes[0] = 'A';
        state::ProcessTokens counters{100, 200};
        MemoryItems input{std::span{file}.first(available)};
        const bool complete = read_entity_items(
            [&](auto count) {
                CHECK(counters ==
                      state::ProcessTokens{100, 200}); // never renewed at city-read entry
                return input.read(count);
            },
            0x2c, destination, counters);
        CHECK(complete == (available == file.size()));
        if (!complete) {
            CHECK(counters == state::ProcessTokens{100, 200});
            if (available >= 30)
                CHECK(destination.units.storage()[0].token == state::UnitToken{50});
            if (available >= 96)
                CHECK(destination.units.storage()[1].damage == 99);
            if (available == 96 + 88 + 33) {
                CHECK(destination.cities.storage()[0].token == state::CityToken{7});
                CHECK(destination.cities.storage()[1].name.bytes[0] == 'B');
                CHECK(destination.cities.storage()[1].token == state::CityToken{0});
            }
        } else {
            CHECK(destination.units.storage()[0].token == state::UnitToken{100});
            CHECK(destination.units.storage()[1].token == state::UnitToken{0});
            CHECK(destination.units.storage()[2].token == state::UnitToken{101});
            CHECK(destination.cities.storage()[0].token == state::CityToken{200});
            CHECK(destination.cities.storage()[1].token == state::CityToken{201});
            CHECK(destination.units.storage()[0].home == state::CitySlot{200});
            CHECK(counters == state::ProcessTokens{102, 202});
        }
    }
    CHECK(file == untouched);
    state::EntityPools empty;
    state::ProcessTokens counters;
    CHECK(read_entity_items(
        [](auto) -> ReadResult {
            FAIL("No zero-size reads");
            return {};
        },
        0x2c, empty, counters));
    CHECK(write_entity_items(
        [](auto) {
            FAIL("No zero-size writes");
            return false;
        },
        empty));
    sizes.clear();
    CHECK_FALSE(write_entity_items(
        [&](auto bytes) {
            sizes.push_back(bytes.size());
            return false;
        },
        source));
    CHECK(sizes == std::vector<std::size_t>{96});
}

TEST_CASE("F06 legacy entity helpers preserve omitted suffixes and use their own version gate",
          "[formats][F06]") {
    for (auto version : std::array<std::uint16_t, 6>{0x26, 0x27, 0x28, 0x29, 0x2a, 0x2b}) {
        state::EntityPools pools;
        pools.units.set_end(2);
        pools.cities.set_end(1);
        for (auto& unit : pools.units.scanned()) {
            unit.token = state::UnitToken{77};
            unit.retained_word = 0xbeef;
        }
        state::ProcessTokens tokens{100, 200};
        std::vector<std::size_t> sizes;
        REQUIRE(read_entity_items(
            [&](auto count) {
                sizes.push_back(count);
                return ReadResult{Bytes(count, 0), true};
            },
            version, pools, tokens));
        if (version < 0x29) {
            CHECK(sizes == std::vector<std::size_t>{26, 26, 84});
            CHECK(pools.units.storage()[0].token == state::UnitToken{100});
            CHECK(pools.units.storage()[1].token == state::UnitToken{101});
            CHECK(pools.cities.storage()[0].token == state::CityToken{200});
            CHECK(tokens == state::ProcessTokens{102, 201});
        } else {
            CHECK(sizes == (version == 0x29 ? std::vector<std::size_t>{30, 30, 88}
                                            : std::vector<std::size_t>{64, 88}));
            CHECK(tokens == state::ProcessTokens{100, 200});
            CHECK(pools.units.storage()[0].token == state::UnitToken{0});
        }
        CHECK(pools.units.storage()[0].retained_word == (version < 0x2a ? 0xbeef : 0));
    }
}

TEST_CASE("F06 raw map decoding preserves stored headers start order and trailing bytes",
          "[formats][F06]") {
    auto bytes = raw_map_vector();
    const auto before = bytes;
    const auto map = decode_raw_map(bytes);
    CHECK(map.header == MapHeader{4, 2, 4, 0x1234, 24, 1, 1});
    CHECK(map.starts.x[0] == 2);
    CHECK(map.starts.y[0] == 1);
    CHECK(map.starts.x[20] == -1);
    CHECK(map.starts.y[20] == -1);
    REQUIRE(map.tiles.size() == 4);
    CHECK(map.tiles[0].terrain_flags == 0xc0);
    CHECK_FALSE(map.tiles[0].owner);
    CHECK(map.tiles[1].ocean());
    CHECK(map.tiles[1].owner == 0);
    CHECK(map.tiles[2].owner == 8);
    CHECK(map.tiles[3].owner == 14);
    CHECK(encode_raw_map(map) == bytes);
    CHECK(bytes == before);
    for (std::size_t length = 0; length < bytes.size(); ++length)
        CHECK_THROWS_AS(decode_raw_map(std::span{bytes}.first(length)), DecodeError);
    bytes[4] = 99;
    bytes[10] = 88;
    bytes[12] = 77;
    bytes.push_back(0xde);
    bytes.push_back(0xad);
    const auto mismatched = decode_raw_map(bytes);
    CHECK(mismatched.header.area == 99);
    CHECK(mismatched.header.coarse_width == 88);
    CHECK(mismatched.tiles.size() == 4);
    CHECK(mismatched.trailing == Bytes{0xde, 0xad});
    CHECK(encode_raw_map(mismatched) == bytes);
    bytes[0] = 3;
    CHECK_THROWS_AS(decode_raw_map(bytes), DecodeError);
}

TEST_CASE("F06 raw map live loading preserves old or new state at each failed item",
          "[formats][F06]") {
    const auto bytes = raw_map_vector();
    // Header short; X short; Y short; tile short; success. Starts can be partial
    // while the old map survives, and a partial tile retains its initialized tail.
    for (const std::size_t available :
         {std::size_t{13}, std::size_t{15}, std::size_t{57}, std::size_t{101}, bytes.size()}) {
        world::Tile fallback;
        fallback.fertility = 7;
        world::MapStorage map{fallback};
        map.allocate(world::Geometry{8, 4});
        map.tile({0, 0}).region = 17;
        map.pattern_seed = 88;
        map.regions[2].fertile = 45;
        map.remembered_barbarian = {43};
        StartingPositions starts;
        starts.x.fill(0x1234);
        starts.y.fill(0x5678);
        MemoryItems input{std::span{bytes}.first(available)};
        std::vector<std::size_t> requests;
        const bool success = read_map_items(
            [&](auto count) {
                requests.push_back(count);
                return input.read(count);
            },
            MapStream::raw, map, starts);
        CHECK(success == (available == bytes.size()));
        CHECK(map.regions[2].fertile == 45);
        CHECK(fallback.fertility == 7);
        CHECK(map.remembered(0, {0, 0}) == 43);
        if (available < 98) {
            CHECK(map.geometry().width() == 8);
            CHECK(map.tile({0, 0}).region == 17);
            CHECK(map.pattern_seed == 88);
            if (available == 13)
                CHECK(starts.x[0] == 0x1234);
            if (available == 15)
                CHECK(starts.x[0] == 0x1202);
            if (available == 57) {
                CHECK(starts.x[0] == 2);
                CHECK(starts.x[1] == -1);
                CHECK(starts.y[0] == 0x5601);
                CHECK(starts.y[1] == 0x5678);
            }
        } else {
            CHECK(requests == std::vector<std::size_t>{14, 42, 42, 24});
            CHECK(map.geometry().area() == 4);
            CHECK(map.pattern_seed == 24);
            CHECK(map.stored_topology == 0x1234);
            CHECK(map.tile({0, 0}).terrain_flags == 0xc0);
            CHECK(map.tile({0, 0}).features == 0x42);
            if (!success) {
                CHECK(map.tile({0, 0}).region == 0);
                CHECK(map.tile({0, 0}).owner == 0);
                CHECK(map.tile({2, 0}) == world::Tile{});
            } else {
                Bytes exported;
                REQUIRE(write_map_items(
                    [&](auto item) {
                        exported.insert(exported.end(), item.begin(), item.end());
                        return true;
                    },
                    MapStream::raw, map, starts));
                CHECK(exported == bytes);
            }
        }
    }
}

TEST_CASE("F06 embedded maps publish memory layers before tiles and recompute sizes",
          "[formats][F06]") {
    const auto raw = raw_map_vector();
    Bytes bytes(raw.begin(), raw.begin() + 14);
    bytes[4] = 99;
    bytes[10] = 88;
    bytes[12] = 77;
    for (Byte civ = 1; civ < 8; ++civ)
        bytes.insert(bytes.end(), 4, civ);
    bytes.insert(bytes.end(), raw.begin() + 98, raw.end());
    world::Tile fallback;
    world::MapStorage map{fallback};
    StartingPositions starts;
    starts.x[0] = 2;
    starts.y[0] = 1;
    MemoryItems input{bytes};
    REQUIRE(read_map_items([&](auto count) { return input.read(count); }, MapStream::embedded, map,
                           starts));
    CHECK(starts == StartingPositions{});
    CHECK(map.geometry().area() == 4);
    CHECK(map.geometry().coarse_width() == 1);
    for (unsigned civ = 1; civ < 8; ++civ)
        CHECK(map.remembered(civ, {0, 0}) == civ);
    CHECK_THROWS_AS(map.coarse(0, {0, 0}), std::logic_error);
    std::vector<Bytes> items;
    REQUIRE(write_map_items(
        [&](auto item) {
            items.emplace_back(item.begin(), item.end());
            return true;
        },
        MapStream::embedded, map, starts));
    REQUIRE(items.size() == 9);
    CHECK(items[0] == Bytes(raw.begin(), raw.begin() + 14));
    CHECK(items[1] == Bytes(4, 1));
    CHECK(items[7] == Bytes(4, 7));
    CHECK(items[8] == Bytes(raw.begin() + 98, raw.end()));
    MemoryItems short_input{std::span{bytes}.first(14 + 4 + 2)};
    CHECK_FALSE(read_map_items([&](auto count) { return short_input.read(count); },
                               MapStream::embedded, map, starts));
    CHECK(map.remembered(1, {2, 1}) == 1);
    CHECK(map.remembered(2, {0, 0}) == 2);
    CHECK(map.remembered(2, {0, 1}) == 0);
    CHECK(map.tile({0, 0}) == world::Tile{});
}

TEST_CASE("F06 map writes stop at failure and allocation errors retain published header",
          "[formats][F06]") {
    const auto raw = raw_map_vector();
    world::Tile fallback;
    world::MapStorage map{fallback};
    StartingPositions starts;
    map.allocate(world::Geometry{8, 4});
    MemoryItems input{raw};
    CHECK_THROWS_AS(read_map_items([&](auto count) { return input.read(count); }, MapStream::raw,
                                   map, starts,
                                   [](auto request) {
                                       if (request.kind == world::StorageKind::remembered)
                                           throw std::bad_alloc{};
                                   }),
                    std::bad_alloc);
    CHECK_FALSE(map.allocated());
    CHECK(map.geometry().area() == 4);
    CHECK(map.pattern_seed == 24);
    CHECK(map.stored_topology == 0x1234);
    CHECK(map.tile({0, 0}) == world::Tile{});
    CHECK(starts.x[0] == 2);
    map.allocate(world::Geometry{4, 2});
    std::vector<std::size_t> writes;
    CHECK_FALSE(write_map_items(
        [&](auto item) {
            writes.push_back(item.size());
            return writes.size() < 2;
        },
        MapStream::raw, map, starts));
    CHECK(writes == std::vector<std::size_t>{14, 42});
    CHECK(map.allocated());
    CHECK(starts.x[0] == 2);
    // A failed replacement can retain the old allocated flag and later layers.
    // Do not silently write their smaller size under the new map header.
    CHECK_THROWS_AS(map.allocate(world::Geometry{8, 4},
                                 [](auto request) {
                                     if (request.kind == world::StorageKind::remembered &&
                                         request.index == 2)
                                         throw std::bad_alloc{};
                                 }),
                    std::bad_alloc);
    writes.clear();
    CHECK_THROWS_AS(write_map_items(
                        [&](auto item) {
                            writes.push_back(item.size());
                            return true;
                        },
                        MapStream::embedded, map, starts),
                    std::invalid_argument);
    CHECK(writes == std::vector<std::size_t>{14, 16});
    CHECK_THROWS_AS(read_map_items([](auto count) { return ReadResult{Bytes(count + 1), false}; },
                                   MapStream::raw, map, starts),
                    std::invalid_argument);
}

TEST_CASE("F06 game header accepts arbitrary separator and distinguishes version classes",
          "[formats][F06]") {
    const Bytes expected{'C', 'I', 'V', 'I', 'L', 'I', 'Z', 'E', 0, 0x1a, 0x2c, 0};
    CHECK(encode_game_header() == expected);
    for (std::size_t length = 0; length < 12; ++length)
        CHECK_THROWS_AS(decode_game_header(std::span{expected}.first(length)), DecodeError);
    auto changed = expected;
    changed[9] = '?';
    CHECK(decode_game_header(changed) == GameHeader{'?', 0x2c});
    changed[0] = 'X';
    CHECK_THROWS_AS(decode_game_header(changed), DecodeError);
    for (auto version : std::array<std::uint16_t, 4>{0x26, 0x27, 0x28, 0x2c})
        CHECK(game_version_status(version) == VersionStatus::supported);
    for (auto version : std::array<std::uint16_t, 5>{0, 0x25, 0x29, 0x2a, 0x2b})
        CHECK(game_version_status(version) == VersionStatus::unsupported_old);
    for (auto version : std::array<std::uint16_t, 2>{0x2d, 0xffff})
        CHECK(game_version_status(version) == VersionStatus::newer);
}
