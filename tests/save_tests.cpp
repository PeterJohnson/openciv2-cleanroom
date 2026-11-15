#include "formats/classic_save.hpp"
#include <catch2/catch_test_macros.hpp>
#include <numeric>

using namespace openciv;
using namespace openciv::formats;
namespace {
// F06 §§2–4 specification-derived vectors, authored without the codecs. Every
// file offset below follows the documented stream/record sizes, not sizeof(T).
void put(Bytes& bytes, std::size_t offset, std::initializer_list<Byte> value) {
    std::copy(value.begin(), value.end(), bytes.begin() + static_cast<std::ptrdiff_t>(offset));
}
Bytes pattern(std::size_t size) {
    Bytes bytes(size);
    std::iota(bytes.begin(), bytes.end(), Byte{0});
    return bytes;
}
constexpr std::size_t map_offset = 12 + 330 + 8 * 242 + 8 * 1428;
constexpr std::size_t entities_offset = map_offset + 14 + 7 * 4 + 6 * 4 + 2 + 1024;
constexpr std::size_t usage_offset = entities_offset + 2 * 32 + 88;
constexpr std::size_t leaders_offset = usage_offset + 63 + 4;
constexpr std::size_t history_offset = leaders_offset + 48;
constexpr std::size_t layout_offset = history_offset + 1200;
constexpr std::size_t views_offset = layout_offset + 106;
constexpr std::size_t elimination_offset = views_offset + 80;
constexpr std::size_t required_end = elimination_offset + 338;
Bytes single_player_vector() {
    Bytes bytes(required_end);
    put(bytes, 0, {'C', 'I', 'V', 'I', 'L', 'I', 'Z', 'E', 0, 0x1a, 0x2c, 0});
    put(bytes, 12 + 0x10, {0xff, 0x7f, 0x00, 0x80}); // turn/year separately
    put(bytes, 12 + 0x2e, {2, 0, 1, 0});             // scan bounds: includes unit hole
    put(bytes, 12 + 330 + 2, {'A', 0, 0xfe});        // terminated name with retained tail
    put(bytes, 12 + 330 + 8 * 242 + 2, {0x00, 0x00, 0x00, 0x80}); // gold
    put(bytes, map_offset, {4, 0, 2, 0, 4, 0, 0, 0, 24, 0, 1, 0, 1, 0});
    bytes[map_offset + 14] = 0xa5;
    bytes[map_offset + 14 + 7 * 4] = 0x8a;
    put(bytes, entities_offset, {2, 0, 1, 0});
    put(bytes, entities_offset + 26, {1, 0, 0, 0x80}); // lifetime retained
    bytes[entities_offset + 32 + 8] = 0xee;            // hole retains payload
    put(bytes, entities_offset + 64 + 84, {0xff, 0xff, 0xff, 0xff});
    put(bytes, usage_offset, {13, 29, 1, 17, 31, 0}); // triplets, not three arrays
    put(bytes, usage_offset + 63, {0xff, 0xff, 2, 0});
    bytes[leaders_offset] = 0x80;
    bytes[history_offset + 149 * 8 + 7] = 0xfe;
    put(bytes, layout_offset + 2, {0xff, 0xff, 0xfe, 0xff, 0x7f, 0, 0x80, 0});
    put(bytes, views_offset + 8, {0xf9, 0xff});
    bytes[elimination_offset + 0x32 + 11 * 24] = 'Z'; // row beyond count retained
    return bytes;
}
template <class Decode, class Encode>
void exact_record(std::size_t size, Decode decode, Encode encode) {
    const auto bytes = pattern(size);
    CHECK(encode(decode(bytes)) == bytes);
    CHECK_THROWS_AS(decode(std::span{bytes}.first(size - 1)), DecodeError);
    auto extra = bytes;
    extra.push_back(0);
    CHECK_THROWS_AS(decode(extra), DecodeError);
}
} // namespace

TEST_CASE("F06 civilization and global codecs retain every byte with exact lengths",
          "[formats][F06]") {
    exact_record(330, decode_globals, encode_globals);
    exact_record(1428, decode_civilization, encode_civilization);
    exact_record(242, decode_civilization_names, encode_civilization_names);
    exact_record(48, decode_leader, encode_leader);
    exact_record(106, decode_layout, encode_layout);
    exact_record(10, decode_map_view, encode_map_view);
    exact_record(100, decode_scenario_parameters, encode_scenario_parameters);
    exact_record(338, decode_elimination_history, encode_elimination_history);
    exact_record(1172, decode_multiplayer, encode_multiplayer);
    exact_record(444, decode_event_record, encode_event_record);
}

TEST_CASE("F02 civilization directed relationships census and goals preserve signed domains",
          "[formats][F02][F06]") {
    auto bytes = pattern(1428);
    put(bytes, 2, {0, 0, 0, 0x80});
    put(bytes, 0x20, {1, 0, 0, 0x80, 0, 0, 0, 0});
    put(bytes, 0x48, {0x80, 0xff});
    put(bytes, 0x66, {0xff, 0xff, 0, 0x80});
    put(bytes, 0x6e, {0xff, 0xff, 0, 0x80});
    put(bytes, 0x192, {0xff, 0xff});
    put(bytes, 0x414, {0xff, 0xff, 0, 0x80, 0xff, 0x80});
    put(bytes, 0x534, {1, 0, 2, 0, 3, 0xff});
    const auto civ = decode_civilization(bytes);
    CHECK(civ.gold == -2147483647 - 1);
    CHECK(civ.tribe == 0x0706);
    CHECK(civ.research_progress == 0x0908);
    CHECK(civ.researching == 0x0b0a);
    CHECK(civ.science_rate == 0x13);
    CHECK(civ.tax_rate == 0x14);
    CHECK(civ.wonders_by_group[3] == 0x1a);
    CHECK(civ.retained_1b == 0x1b);
    CHECK(civ.diplo_request_count == 0x1f);
    CHECK(civ.treaties[0] == 0x80000001);
    CHECK(civ.treaties[1] == 0);
    CHECK(civ.attitude[7] == 0x47);
    CHECK(civ.reputation_credit[0] == -128);
    CHECK(civ.reputation_credit[1] == -1);
    CHECK(civ.aggression_counter[7] == 0x57);
    CHECK(civ.advances[12] == 0x64);
    CHECK(civ.retained_65 == 0x65);
    CHECK(civ.military_count == -1);
    CHECK(civ.city_count == -32768);
    CHECK(civ.military_strength == 65535);
    CHECK(civ.total_attack == -32768);
    CHECK(civ.advance_source[99] == 0xd7);
    CHECK(civ.units_active[61] == 0x15);
    CHECK(civ.units_lost[61] == 0x53);
    CHECK(civ.units_in_production[61] == 0x91);
    CHECK(civ.continent_military[0] == 65535);
    CHECK(civ.continent_attack[0] == 0x1312);
    CHECK(civ.continent_cities[63] == 0xd1);
    CHECK(civ.continent_city_size[63] == 0x11);
    CHECK(civ.continent_trade_units[63] == 0x51);
    CHECK(civ.continent_flags[63] == 0x91);
    CHECK(civ.continent_policy[63] == 0xd1);
    CHECK(civ.best_shield_surplus == -11310);
    CHECK(civ.government_score[6] == -7712);
    CHECK(civ.last_contact_turn[7] == -3600);
    CHECK(civ.leader_portrait == 0xf2);
    CHECK(civ.throne_room[9] == 0xfc);
    CHECK(civ.retained_3fd == 0xfd);
    CHECK(civ.throne_room_upgrades == -2);
    CHECK(civ.spaceship_flags == 0);
    CHECK(civ.retained_401 == 1);
    CHECK(civ.spaceship_arrival_year == 0x0302);
    CHECK(civ.spaceship_parts[5] == 0x1312);
    CHECK(civ.goals[0] == state::CivilizationGoal{-1, -32768, -1, -128});
    CHECK(civ.standing_goals[0] == state::CivilizationGoal{1, 2, 3, -1});
    CHECK(civ.standing_goals[15].priority == -109);
    CHECK(encode_civilization(civ) == bytes);
}

TEST_CASE("F06 globals retain contextual selectors ranks and wonder sentinels", "[formats][F06]") {
    auto bytes = pattern(330);
    put(bytes, 0x16, {0xfe, 0xff, 0xff, 0xff});
    put(bytes, 0x1b, {0xff, 0x80, 0, 7});
    put(bytes, 0xfe, {0xff, 0xff, 0xfe, 0xff, 0x80, 0});
    const auto v = decode_globals(bytes);
    CHECK(v.game_options == 0x05040302);
    CHECK(v.active_unit.value == -2);
    CHECK(v.moving_unit.value == -1);
    CHECK(v.player_civ == -1);
    CHECK(v.saved_current_civ == -128);
    CHECK(v.turn_civ == 0);
    CHECK(v.view_civ == 7);
    CHECK(v.unit_end == 0x2f2e);
    CHECK(v.city_end == 0x3130);
    CHECK(v.first_discoverer[99] == 0x99);
    CHECK(v.advance_known_by[99] == 0xfd);
    CHECK(v.wonder_city[0] == -1);
    CHECK(v.wonder_city[1] == -2);
    CHECK(v.wonder_city[2] == 128);
    CHECK(v.next_history_turn == 0x3736);
    CHECK(v.power_rank[7] == 0x41);
    CHECK(v.civ_by_rank[7] == 0x49);
    CHECK(encode_globals(v) == bytes);
}

TEST_CASE("F06 trailing records expose signed fields strings and retained payload",
          "[formats][F06]") {
    const auto leader = decode_leader(pattern(48));
    CHECK(leader.city_name_index == 5);
    CHECK(leader.names[1] == 0x1312);
    CHECK(leader.titles[6][1] == 0x2f2e);
    auto names_bytes = pattern(242);
    names_bytes[3] = 0;
    const auto names = decode_civilization_names(names_bytes);
    CHECK(names.leader.text().size() == 1);
    CHECK(names.leader.bytes[2] == 4);
    CHECK(names.tribe.bytes[0] == 0x1a);
    CHECK(names.adjective.bytes[0] == 0x32);
    CHECK(names.titles[6].bytes[23] == 0xf1);
    const auto scenario = decode_scenario_parameters(pattern(100));
    CHECK(scenario.name.bytes[79] == 0x51);
    CHECK(scenario.technology_paradigm == 0x5352);
    CHECK(scenario.victory_thresholds[3] == 0x6362);
    const auto history = decode_elimination_history(pattern(338));
    CHECK(history.turns[11] == 0x1918);
    CHECK(history.killers[11] == 0x25);
    CHECK(history.portraits[11] == 0x31);
    CHECK(history.names[11].bytes[23] == 0x51);
    const auto mp = decode_multiplayer(pattern(1172));
    CHECK(mp.turn_time_limit == 0x33323130);
    CHECK(mp.encoded_passwords[255] == 0x33);
    CHECK(mp.simultaneous == 0x3d3c);
    CHECK(mp.cheat_turn_time == 0x41403f3e);
    CHECK(mp.retained_142[289] == 0x63);
    CHECK(mp.emails[7][31] == 0x63);
    CHECK(mp.player_setup[7][31] == 0x63);
    CHECK(mp.resume_civ == 0x64);
    CHECK(mp.double_production == 0x6d6c);
    CHECK(mp.double_movement == 0x6f6e);
    CHECK(mp.claimed_humans == 0x7170);
    CHECK(mp.retained_472[33] == 0x93);
    const auto event = decode_event_record(pattern(444));
    CHECK(event.unit_name == 0x0b0a0908);
    CHECK(event.text[19] == 0x87868584);
    CHECK(event.move_rectangle[7] == static_cast<std::int32_t>(-1145390664));
    CHECK(event.create_locations[19] == 0x33323130);
    CHECK(event.auxiliary_scalar == 0x47464544);
    CHECK(event.retained[39] == 0x73);
    CHECK(event.wave_file == 0x87868584);
    CHECK(event.give_tech_civ == static_cast<std::int32_t>(-1145390664));
}

TEST_CASE("F06 stored save preserves exact stream order holes tokens and source bytes",
          "[formats][F06]") {
    auto bytes = single_player_vector();
    bytes[9] = 0x55;                            // reader accepts a separator other than ^Z
    bytes.insert(bytes.end(), {'E', 'V', 'N'}); // partial optional tag is absent
    const auto original = bytes;
    const auto save = decode_stored_save(bytes);
    CHECK(save.globals.turn == 32767);
    CHECK(save.globals.year == -32768);
    CHECK(save.names[0].leader.bytes[2] == 0xfe);
    CHECK(save.civilizations[0].gold == -2147483647 - 1);
    CHECK(save.map.remembered[0][0] == 0xa5);
    CHECK(save.map.tiles[0].terrain == world::Terrain::ocean);
    REQUIRE(save.units.size() == 2);
    CHECK(save.units[0].token == state::UnitToken{0x80000001});
    CHECK(save.units[1].token == state::UnitToken{0});
    CHECK(save.units[1].moves_used == 0xee);
    CHECK(save.cities[0].token == state::CityToken{0xffffffff});
    CHECK(save.tribes[0] == TribeUsage{13, 29, 1});
    CHECK(save.tribes[1] == TribeUsage{17, 31, 0});
    CHECK(save.cursor_x == -1);
    CHECK(save.cursor_y == 2);
    REQUIRE(save.leaders.size() == 1);
    CHECK(save.leaders[0].attack == -128);
    CHECK(save.power_history[149][7] == 254);
    CHECK(save.layout.rectangles[0] == SavedRectangle{-1, -2, 127, 128});
    CHECK(save.views[0].zoom == -7);
    CHECK(save.eliminations.count == 0);
    CHECK(save.eliminations.names[11].bytes[0] == 'Z');
    CHECK_FALSE(save.scenario);
    CHECK_FALSE(save.multiplayer);
    CHECK_FALSE(save.events);
    CHECK(save.trailing == Bytes{'E', 'V', 'N'});
    CHECK(encode_stored_save(save) == original);
    CHECK(bytes == original);
}

TEST_CASE("F06 stored saves select scenario and multiplayer blocks from stored flags",
          "[formats][F06]") {
    auto bytes = single_player_vector();
    bytes[12 + 8] = 0x40;    // saved-as-scenario alone is sufficient
    bytes[12 + 0x1a] = 1;    // hotseat
    bytes[12 + 0x23] = 0x85; // three human slots including zero
    // Tribes all remain zero, so these are repeated tribe records, not a set.
    bytes.insert(bytes.begin() + history_offset, 96, 0);
    bytes[leaders_offset + 48] = 17;
    bytes[leaders_offset + 96] = 23;
    bytes.insert(bytes.begin() + elimination_offset + 96, 100, 0);
    bytes[elimination_offset + 96 + 2] = 'S';
    bytes.insert(bytes.end(), 1172, 0);
    bytes[bytes.size() - 1172 + 0x46e] = 1;
    const auto save = decode_stored_save(bytes);
    REQUIRE(save.leaders.size() == 3);
    CHECK(save.leaders[0].attack == -128);
    CHECK(save.leaders[1].attack == 17);
    CHECK(save.leaders[2].attack == 23);
    REQUIRE(save.scenario);
    CHECK(save.scenario->name.bytes[0] == 'S');
    REQUIRE(save.multiplayer);
    CHECK(save.multiplayer->double_movement == 1);
    CHECK(encode_stored_save(save) == bytes);
    // No human bits in a nonzero mode means no leader records.
    bytes[12 + 0x23] = 0;
    bytes.erase(bytes.begin() + leaders_offset, bytes.begin() + leaders_offset + 144);
    CHECK(decode_stored_save(bytes).leaders.empty());
    CHECK(encode_stored_save(decode_stored_save(bytes)) == bytes);
}

TEST_CASE("F06 event block retains presence words empty strings and all-records-first order",
          "[formats][F06]") {
    auto bytes = single_player_vector();
    bytes.insert(bytes.end(), {'E', 'V', 'N', 'T', 2, 0, 0, 0});
    const auto first = bytes.size();
    bytes.insert(bytes.end(), 2 * 444, 0);
    put(bytes, first + 8, {0x01, 0x02, 0x03, 0x84}); // arbitrary nonzero token
    bytes[first + 0x38] = 2;
    bytes[first + 0x3c] = 3;
    bytes[first + 0x140] = 4;
    bytes[first + 0x184] = 5;
    bytes[first + 444 + 0x10] = 6;
    put(bytes, first + 444 + 0x17c, {0, 0, 0, 0x80});
    bytes.insert(bytes.end(), {0, 'A', 0, 'B', 0, 'C', 0, 'W', 0, 'D', 0});
    bytes.insert(bytes.end(), {0xfa, 0xfb});
    const auto save = decode_stored_save(bytes);
    REQUIRE(save.events);
    REQUIRE(save.events->entries.size() == 2);
    const auto& a = save.events->entries[0];
    CHECK(a.record.unit_name == 0x84030201);
    CHECK(a.strings.names[0] == std::optional{Bytes{}});
    CHECK_FALSE(a.strings.names[1]);
    CHECK(a.strings.text[0] == std::optional{Bytes{'A'}});
    CHECK(a.strings.text[1] == std::optional{Bytes{'B'}});
    CHECK_FALSE(a.strings.text[2]);
    CHECK(a.strings.other[7] == std::optional{Bytes{'C'}});
    CHECK(a.strings.other[10] == std::optional{Bytes{'W'}});
    CHECK(save.events->entries[1].strings.names[1] == std::optional{Bytes{'D'}});
    CHECK(save.events->entries[1].record.money_amount == -2147483647 - 1);
    CHECK(save.trailing == Bytes{0xfa, 0xfb});
    CHECK(encode_stored_save(save) == bytes);
    // Required fixed blocks and every string prefix are truncation errors.
    for (std::size_t end = required_end + 4; end < bytes.size() - 2; ++end)
        CHECK_THROWS_AS(decode_stored_save(std::span{bytes}.first(end)), DecodeError);
    auto bad = bytes;
    bad[first + 0x44] = 1; // nonzero after first absent text line
    CHECK_THROWS_AS(decode_stored_save(bad), DecodeError);
    put(bad, required_end + 4, {0xff, 0xff, 0xff, 0x7f});
    CHECK_THROWS_AS(decode_stored_save(bad), DecodeError);
    put(bad, required_end + 4, {0xff, 0xff, 0xff, 0xff});
    CHECK_THROWS_AS(decode_stored_save(bad), DecodeError);
}

TEST_CASE("F06 stored save rejects truncated required blocks unsupported versions and bounds",
          "[formats][F06]") {
    const auto bytes = single_player_vector();
    // Each byte boundary, including mid-record, must fail without touching input.
    for (std::size_t end = 0; end < bytes.size(); ++end)
        CHECK_THROWS_AS(decode_stored_save(std::span{bytes}.first(end)), DecodeError);
    for (Byte version : std::array<Byte, 5>{0x25, 0x29, 0x2a, 0x2b, 0x2d}) {
        auto bad = bytes;
        bad[10] = version;
        CHECK_THROWS_AS(decode_stored_save(bad), DecodeError);
    }
    for (auto offset : {12U + 0x2e, 12U + 0x30}) {
        auto bad = bytes;
        put(bad, offset, {0xff, 0xff});
        try {
            decode_stored_save(bad);
            FAIL("Negative scan bound accepted");
        } catch (const DecodeError& error) {
            CHECK(error.offset() == offset);
        }
    }
    auto bad = bytes;
    bad[map_offset] = 3;
    CHECK_THROWS_AS(decode_stored_save(bad), DecodeError);
    // Stored size disagreements remain inspectable and do not change windows.
    bad = bytes;
    put(bad, map_offset + 4, {0xff, 0xff});
    put(bad, map_offset + 10, {0, 0, 0, 0});
    CHECK(decode_stored_save(bad).map.header.area == -1);
    CHECK(encode_stored_save(decode_stored_save(bad)) == bad);
}

TEST_CASE("F06 stored saves bound allocation and preserve zero and full entity scans",
          "[formats][F06]") {
    for (const auto bounds : std::array{std::pair{0, 0}, std::pair{2048, 256}}) {
        auto bytes = single_player_vector();
        bytes.erase(bytes.begin() + entities_offset, bytes.begin() + usage_offset);
        bytes.insert(bytes.begin() + entities_offset,
                     static_cast<std::size_t>(bounds.first * 32 + bounds.second * 88), 0);
        put(bytes, 12 + 0x2e,
            {static_cast<Byte>(bounds.first), static_cast<Byte>(bounds.first >> 8),
             static_cast<Byte>(bounds.second), static_cast<Byte>(bounds.second >> 8)});
        const auto save = decode_stored_save(bytes);
        CHECK(save.units.size() == static_cast<std::size_t>(bounds.first));
        CHECK(save.cities.size() == static_cast<std::size_t>(bounds.second));
        CHECK(save.tribes[0] == TribeUsage{13, 29, 1});
        CHECK(encode_stored_save(save) == bytes);
    }
    auto bytes = single_player_vector();
    put(bytes, 12 + 0x2e, {1, 8}); // 2049
    CHECK_THROWS_AS(decode_stored_save(bytes), DecodeError);
    bytes = single_player_vector();
    put(bytes, 12 + 0x30, {1, 1}); // 257
    CHECK_THROWS_AS(decode_stored_save(bytes), DecodeError);
    bytes = single_player_vector();
    bytes.insert(bytes.end(), {'E', 'V', 'N', 'T', 0, 0, 0, 0});
    const auto empty_events = decode_stored_save(bytes);
    REQUIRE(empty_events.events);
    CHECK(empty_events.events->entries.empty());
    CHECK(encode_stored_save(empty_events) == bytes);
    bytes = single_player_vector();
    bytes.insert(bytes.end(), {'E', 'V', 'N', 'T', 1, 0, 0, 0});
    bytes.insert(bytes.end(), 444, 0);
    bytes[required_end + 8 + 8] = 1;
    bytes.insert(bytes.end(), 255, 0xfe);
    bytes.push_back(0);
    CHECK(encode_stored_save(decode_stored_save(bytes)) == bytes);
    bytes.back() = 0xfe;
    bytes.push_back(0);
    CHECK_THROWS_AS(decode_stored_save(bytes), DecodeError);
}

TEST_CASE("F06 stored encoding rejects inconsistent views instead of silently repairing them",
          "[formats][F06]") {
    const auto original = decode_stored_save(single_player_vector());
    auto save = original;
    save.civilizations[0].gold = 123;
    CHECK(original.civilizations[0].gold == -2147483647 - 1);
    save.civilizations.pop_back();
    CHECK_THROWS_AS(encode_stored_save(save), std::invalid_argument);
    save = original;
    save.units.pop_back();
    CHECK_THROWS_AS(encode_stored_save(save), std::invalid_argument);
    save = original;
    save.leaders.clear();
    CHECK_THROWS_AS(encode_stored_save(save), std::invalid_argument);
    save = original;
    save.scenario.emplace();
    CHECK_THROWS_AS(encode_stored_save(save), std::invalid_argument);
    save = original;
    save.map.remembered[6].clear();
    CHECK_THROWS_AS(encode_stored_save(save), std::invalid_argument);
    save = original;
    save.events.emplace().entries.emplace_back().record.unit_name = 1;
    CHECK_THROWS_AS(encode_stored_save(save), std::invalid_argument);
    auto& text = save.events->entries[0].strings.names[0];
    text = Bytes(255, 0xfe);
    CHECK(decode_stored_save(encode_stored_save(save)) == save);
    text->push_back(0xfe);
    CHECK_THROWS_AS(encode_stored_save(save), std::invalid_argument);
    text = Bytes{'A', 0, 'B'};
    CHECK_THROWS_AS(encode_stored_save(save), std::invalid_argument);
    save = original;
    save.trailing = {'E', 'V', 'N', 'T'};
    CHECK_THROWS_AS(encode_stored_save(save), std::invalid_argument);
}
