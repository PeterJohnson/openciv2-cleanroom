#include "formats/state_conversion.hpp"
#include "formats/preference_io.hpp"
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

TEST_CASE("Classic stored preference bits are separate from semantic live options",
          "[formats][preferences]") {
    formats::StoredMultiplayerPreferences stored;
    stored.open_game = -32768;
    stored.double_movement = 17;
    stored.clear_chat_boot = -1;
    stored.game_name.bytes = {'G', 0, 'X'};
    stored.legacy_settings[0].fill(0xa5);
    const auto bytes = formats::encode_multiplayer_preferences(stored);
    CHECK(formats::encode_multiplayer_preferences(formats::decode_multiplayer_preferences(bytes)) ==
          bytes);
    const auto live = formats::to_live(stored);
    CHECK(live.open_game);
    CHECK(live.double_movement);
    CHECK(live.clear_chat_boot);
    CHECK(live.game_name == "G");
    const auto exported = formats::to_stored(live);
    CHECK(exported.open_game == 1);
    CHECK(exported.double_movement == 1);
    CHECK(exported.game_name.bytes[2] == 0);
    CHECK(exported.legacy_settings[0] == std::array<std::uint8_t, 24>{});
}
TEST_CASE("Live text is unconstrained by classic storage and export never truncates",
          "[formats][state]") {
    state::City city;
    city.name = "A much longer city name";
    CHECK(city.name == "A much longer city name");
    CHECK_THROWS_AS(formats::encode_city(city), std::invalid_argument);
    city.name = "Short";
    const auto stored = formats::decode_city(formats::encode_city(city));
    CHECK(formats::to_live(stored).name == "Short");
    city.name = std::string("A\0B", 3);
    CHECK_THROWS_AS(formats::encode_city(city), std::invalid_argument);
    state::CivilizationNames names;
    names.leader = std::string(40, 'L');
    CHECK_THROWS_AS(formats::encode_civilization_names(names), std::invalid_argument);
    state::ScenarioParameters scenario;
    scenario.name = std::string(81, 'S');
    CHECK_THROWS_AS(formats::encode_scenario_parameters(scenario), std::invalid_argument);
}
TEST_CASE("Named setup choices and live flags convert at classic boundaries", "[formats][state]") {
    state::OrdinaryPreferences preferences;
    preferences.difficulty = state::Difficulty::deity;
    preferences.gender = state::Gender::female;
    preferences.map_size = state::WorldSize::large;
    preferences.rules_choice = state::RuleSelection::advanced;
    const auto stored = formats::to_stored(preferences);
    CHECK(stored.difficulty == 5);
    CHECK(stored.gender == 1);
    CHECK(stored.map_size == 2);
    CHECK(stored.rules_choice == 1);
    CHECK(formats::to_live(stored) == preferences);
    state::Civilization civilization;
    civilization.government = state::Government::democracy;
    CHECK(formats::to_stored(civilization).government == 6);
    CHECK(formats::to_live(formats::to_stored(civilization)) == civilization);
    formats::SavedMapView view{-32768, 19, 4, 5, 6};
    const auto live = formats::to_live(view);
    CHECK(live.open);
    CHECK(live.track_moves);
    CHECK(formats::to_stored(live) == formats::SavedMapView{1, 1, 4, 5, 6});
}
