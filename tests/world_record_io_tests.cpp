// Independently authored expectations from F06 sections 2, 3, 6 and 7.
#include "formats/world_record_io.hpp"
#include "state/game.hpp"
#include <catch2/catch_test_macros.hpp>
#include <bit>

using namespace openciv;
using namespace openciv::formats;
namespace {
struct Records {
    state::GlobalValues globals;
    std::array<state::CivilizationNames, 8> names;
    std::array<state::Civilization, 8> civs;
    bool scenario = true;
    bool read(const ReadItem& read, std::uint16_t version = 0x2c) {
        return read_persistent_items(read, version, globals, names, civs, scenario);
    }
};
std::unique_ptr<Records> incoming() {
    auto r = std::make_unique<Records>();
    r->globals = decode_globals(Bytes(global_record_size, 0xa5));
    for (auto& name : r->names)
        name = decode_civilization_names(Bytes(civilization_names_size, 0xa5));
    for (auto& civ : r->civs)
        civ = decode_civilization(Bytes(civilization_record_size, 0xa5));
    return r;
}
std::unique_ptr<Records> saved() {
    auto r = std::make_unique<Records>();
    r->globals.civs_alive = 0x87; // Barbarian and three nonbarbarian civilizations.
    r->globals.num_enemies = 89;
    r->globals.turn = 1234;
    r->globals.game_options = 0x800020U;
    for (std::size_t i = 0; i < 100; ++i) {
        r->globals.first_discoverer[i] = static_cast<Byte>(i + 1);
        r->globals.advance_known_by[i] = static_cast<Byte>(i + 101);
    }
    for (std::size_t i = 0; i < 8; ++i) {
        r->names[i].leader.bytes.fill(static_cast<Byte>(i + 11));
        auto& civ = r->civs[i];
        civ.tribe = static_cast<std::int16_t>(i);
        civ.gold = static_cast<std::int32_t>(i + 1000);
        civ.retained_65 = 0x42;
        civ.advances.fill(0xfe);
        civ.advance_source.fill(0x37);
        civ.units_active.fill(0x48);
        civ.units_lost.fill(0x59);
        civ.units_in_production.fill(0x6a);
        civ.continent_military.fill(0x1234);
        civ.standing_goals[15].priority = -127;
    }
    return r;
}
// Author the wire layout from F06 section 7's compact-record table. Keep the
// discarded input byte distinct from all incoming fields and supplied payload.
std::vector<Bytes> items(const Records& r, std::uint16_t version) {
    std::vector<Bytes> out;
    const auto global = encode_globals(r.globals);
    if (version < 0x28) {
        for (const auto [start, end] :
             {std::pair{0x000, 0x036}, {0x036, 0x093}, {0x09a, 0x0f7}, {0x0fe, 0x14a}})
            out.emplace_back(global.begin() + start, global.begin() + end);
    } else {
        out.push_back(global);
    }
    ByteWriter names;
    for (const auto& name : r.names)
        names.append(encode_civilization_names(name));
    out.push_back(std::move(names.bytes));
    ByteWriter civilizations;
    for (const auto& civ : r.civs) {
        const auto bytes = encode_civilization(civ);
        if (version < 0x28) {
            for (const auto [start, end] : {std::pair{0x000, 0x058},
                                            {0x058, 0x064},
                                            {0x066, 0x074},
                                            {0x074, 0x0d1},
                                            {0x0d8, 0x10e},
                                            {0x116, 0x14c},
                                            {0x154, 0x18a}})
                out.emplace_back(bytes.begin() + start, bytes.begin() + end);
            out.push_back({0x99});
            out.emplace_back(bytes.begin() + 0x192, bytes.end());
        } else {
            civilizations.append(bytes);
        }
    }
    if (version >= 0x28)
        out.push_back(std::move(civilizations.bytes));
    return out;
}
Bytes joined(const std::vector<Bytes>& items) {
    ByteWriter writer;
    for (const auto& item : items)
        writer.append(item);
    return std::move(writer.bytes);
}
} // namespace

TEST_CASE("F06 current persistent records publish three items before scenario normalization",
          "[formats][world-record-io]") {
    for (const auto version : {0x28, 0x2c}) {
        auto source = saved(), target = incoming();
        source->globals.multiplayer = 3;
        source->globals.game_flags = 0x725f;
        source->globals.game_options |= 0x8000;
        source->globals.reveal_map = 5;
        const auto wire = items(*source, static_cast<std::uint16_t>(version));
        std::size_t call = 0;
        REQUIRE(target->read(
            [&](std::size_t size) {
                REQUIRE(call < wire.size());
                REQUIRE(size == wire[call].size());
                if (call != 0) {
                    REQUIRE(target->globals.num_enemies == 2);
                    REQUIRE(target->globals.game_flags == 0x725f);
                    REQUIRE(target->globals.reveal_map == 5);
                    REQUIRE((target->globals.game_options & 0x8000U) != 0);
                }
                return ReadResult{wire[call++], true};
            },
            static_cast<std::uint16_t>(version)));
        REQUIRE(call == 3);
        REQUIRE(target->names == source->names);
        REQUIRE(target->civs == source->civs);
        REQUIRE(target->globals.first_discoverer == source->globals.first_discoverer);
        REQUIRE(target->scenario);
        REQUIRE(target->globals.game_flags == 0x728f);
        REQUIRE(target->globals.game_options == 0x800020);
        REQUIRE(target->globals.reveal_map == 0);
    }
}
TEST_CASE("F06 old persistent records clear omitted arrays and retain the unrepresented byte",
          "[formats][world-record-io]") {
    for (const auto version : {0x26, 0x27}) {
        auto source = saved(), target = incoming();
        const auto wire = items(*source, static_cast<std::uint16_t>(version));
        REQUIRE(wire.size() == 77);
        REQUIRE(joined(wire).size() == 0x13c + 0x790 + 8 * 0x574);
        std::size_t call = 0;
        REQUIRE(target->read(
            [&](std::size_t size) {
                REQUIRE(size == wire.at(call).size());
                if (call == 0) {
                    REQUIRE(target->globals.first_discoverer[0] == 0);
                    REQUIRE(target->globals.advance_known_by[99] == 0);
                    REQUIRE(target->civs[7].units_lost[61] == 0);
                    REQUIRE(target->civs[7].advances[12] == 0);
                    REQUIRE(target->civs[7].advance_source[99] == 0);
                    REQUIRE(target->civs[7].gold == std::bit_cast<std::int32_t>(0xa5a5a5a5U));
                }
                if (call == 4)
                    REQUIRE(target->globals.num_enemies == 2);
                return ReadResult{wire[call++], true};
            },
            static_cast<std::uint16_t>(version)));
        REQUIRE(call == wire.size());
        REQUIRE_FALSE(target->scenario);
        REQUIRE(target->names == source->names);
        REQUIRE(target->globals.turn == 1234);
        REQUIRE(target->globals.first_discoverer[92] == 93);
        REQUIRE(target->globals.first_discoverer[93] == 0);
        REQUIRE(target->globals.advance_known_by[92] == 193);
        REQUIRE(target->globals.advance_known_by[99] == 0);
        for (std::size_t i = 0; i < 8; ++i) {
            const auto& civ = target->civs[i];
            REQUIRE(civ.gold == static_cast<std::int32_t>(i + 1000));
            REQUIRE(civ.advances[11] == 0xfe);
            REQUIRE(civ.advances[12] == 0);
            REQUIRE(civ.retained_65 == 0xa5);
            REQUIRE(civ.advance_source[92] == 0x37);
            REQUIRE(civ.advance_source[93] == 0);
            REQUIRE(civ.units_active[53] == 0x48);
            REQUIRE(civ.units_active[54] == 0);
            REQUIRE(civ.units_lost[53] == 0x59);
            REQUIRE(civ.units_lost[61] == 0);
            REQUIRE(civ.units_in_production[53] == 0x6a);
            REQUIRE(civ.units_in_production[54] == 0);
            REQUIRE(civ.continent_military[0] == 0x1234);
            REQUIRE(civ.standing_goals[15].priority == -127);
        }
    }
}
TEST_CASE("F06 incomplete game blocks preserve prefixes and postpone enemy-count recomputation",
          "[formats][world-record-io]") {
    auto source = saved();
    const auto bytes = encode_globals(source->globals);
    for (std::size_t length = 0; length <= bytes.size(); ++length) {
        auto target = incoming();
        unsigned calls = 0;
        REQUIRE_FALSE(target->read([&](std::size_t size) {
            ++calls;
            REQUIRE(size == bytes.size());
            return ReadResult{{bytes.begin(), bytes.begin() + static_cast<std::ptrdiff_t>(length)},
                              false};
        }));
        REQUIRE(calls == 1);
        Bytes expected(bytes.size(), 0xa5);
        std::copy_n(bytes.begin(), length, expected.begin());
        REQUIRE(encode_globals(target->globals) == expected);
        REQUIRE(target->civs[7].units_lost[61] == 0xa5);
        REQUIRE(target->names[0].leader.bytes[0] == 0xa5);
        REQUIRE(target->scenario); // Neither a result nor flag fixup was published.
    }
}
TEST_CASE("F06 partial array items publish across records without clearing unread fields",
          "[formats][world-record-io]") {
    auto source = saved();
    const auto wire = items(*source, 0x2c);
    for (const std::size_t failed : {1U, 2U})
        for (const std::size_t length : {0U, 1U, 241U, 242U, 243U, 1427U, 1428U, 1429U}) {
            auto target = incoming();
            std::size_t call = 0;
            REQUIRE_FALSE(target->read([&](std::size_t size) {
                const auto item = call++;
                REQUIRE(size == wire.at(item).size());
                if (item == failed)
                    return ReadResult{{wire[item].begin(),
                                       wire[item].begin() + static_cast<std::ptrdiff_t>(length)},
                                      false};
                return ReadResult{wire[item], true};
            }));
            REQUIRE(call == failed + 1);
            REQUIRE(target->globals.num_enemies == 2);
            ByteWriter actual;
            if (failed == 1) {
                for (const auto& name : target->names)
                    actual.append(encode_civilization_names(name));
                REQUIRE(target->civs[0].units_lost[0] == 0xa5);
            } else {
                REQUIRE(target->names == source->names);
                for (const auto& civ : target->civs)
                    actual.append(encode_civilization(civ));
            }
            Bytes expected(wire[failed].size(), 0xa5);
            std::copy_n(wire[failed].begin(), length, expected.begin());
            REQUIRE(actual.bytes == expected);
        }
}
TEST_CASE("F06 old reads stop at each failed item including the discarded byte",
          "[formats][world-record-io]") {
    auto source = saved();
    const auto wire = items(*source, 0x26);
    for (std::size_t failed = 0; failed < wire.size(); ++failed) {
        auto target = incoming();
        std::size_t call = 0;
        REQUIRE_FALSE(target->read(
            [&](std::size_t size) {
                const auto item = call++;
                REQUIRE(size == wire.at(item).size());
                return ReadResult{wire[item],
                                  item != failed}; // Failed full transfer still publishes.
            },
            0x26));
        REQUIRE(call == failed + 1);
        REQUIRE(target->scenario);
        REQUIRE(target->globals.first_discoverer[99] == 0);
        REQUIRE(target->globals.advance_known_by[99] == 0);
        REQUIRE(target->globals.num_enemies == (failed < 4 ? 89 : 2));
        for (std::size_t civ = 0; civ < 8; ++civ) {
            const auto prefix_item = 5 + civ * 9;
            REQUIRE(target->civs[civ].gold == (failed >= prefix_item
                                                   ? static_cast<std::int32_t>(civ + 1000)
                                                   : std::bit_cast<std::int32_t>(0xa5a5a5a5U)));
            REQUIRE(target->civs[civ].units_lost[61] == 0);
            REQUIRE(target->civs[civ].retained_65 == 0xa5);
            REQUIRE(target->civs[civ].standing_goals[15].priority ==
                    (failed >= prefix_item + 8 ? -127 : -91));
        }
    }
    // A genuinely short segment retains the precleared suffix, not source or incoming bytes.
    auto target = incoming();
    std::size_t call = 0;
    REQUIRE_FALSE(target->read(
        [&](std::size_t) {
            if (call++ == 1)
                return ReadResult{{1, 2, 3}, false};
            return ReadResult{wire[0], true};
        },
        0x27));
    REQUIRE(target->globals.first_discoverer[2] == 3);
    REQUIRE(target->globals.first_discoverer[3] == 0);
    REQUIRE(target->globals.num_enemies == 89);
}
TEST_CASE("F06 persistent callbacks preserve live untransferred fields and later write operands",
          "[formats][world-record-io]") {
    auto target = incoming();
    REQUIRE_FALSE(target->read([&](std::size_t) {
        target->globals.rules = 0xabcd;
        target->globals.turn = 123;
        return ReadResult{{0x34}, false};
    }));
    REQUIRE(target->globals.rules == 0xab34);
    REQUIRE(target->globals.turn == 123);
    unsigned calls = 0;
    REQUIRE_FALSE(write_persistent_items(
        [&](std::span<const Byte> bytes) {
            ++calls;
            if (calls == 1) {
                target->names[0].city_style = 321;
                return true;
            }
            REQUIRE(bytes.size() == 8 * civilization_names_size);
            REQUIRE(decode_civilization_names(bytes.first(civilization_names_size)).city_style ==
                    321);
            target->civs[0].gold = 999;
            return false;
        },
        target->globals, target->names, target->civs));
    REQUIRE(calls == 2);
    calls = 0;
    REQUIRE(write_persistent_items(
        [&](std::span<const Byte> bytes) {
            ++calls;
            if (calls == 2)
                target->civs[0].gold = 123456;
            if (calls == 3) {
                REQUIRE(bytes.size() == 8 * civilization_record_size);
                REQUIRE(decode_civilization(bytes.first(civilization_record_size)).gold == 123456);
            }
            return true;
        },
        target->globals, target->names, target->civs));
    REQUIRE(calls == 3);
}
TEST_CASE("F06 scenario and enemy-count adjustments retain unrelated flags and wrap empty masks",
          "[formats][world-record-io]") {
    for (const unsigned mode : {0U, 4U})
        for (const unsigned flags : {0x8110U, 0x8150U, 0x8190U, 0x81d0U})
            for (const unsigned alive : {0U, 1U, 2U, 3U, 0xffU}) {
                auto source = saved(), target = incoming();
                source->globals.multiplayer = static_cast<Byte>(mode);
                source->globals.game_flags = static_cast<std::uint16_t>(flags);
                source->globals.civs_alive = static_cast<Byte>(alive);
                source->globals.game_options = 0x12348000;
                source->globals.reveal_map = 0xff;
                const auto wire = joined(items(*source, 0x2c));
                MemoryItems memory(wire);
                REQUIRE(target->read([&](std::size_t size) { return memory.read(size); }));
                const bool exported = (flags & 0x40U) != 0;
                REQUIRE(target->scenario == ((flags & 0xc0U) != 0));
                REQUIRE(target->globals.reveal_map == (exported ? 0 : 0xff));
                REQUIRE(target->globals.game_options ==
                        (exported || mode ? 0x12340000U : 0x12348000U));
                REQUIRE(target->globals.game_flags == (exported ? 0x8180U : flags));
                REQUIRE(target->globals.num_enemies == (alive == 0 || alive == 1 ? 255
                                                        : alive == 0xff          ? 6
                                                                                 : 0));
            }
}
TEST_CASE("F06 invalid core versions and item contracts stop without invented load statuses",
          "[formats][world-record-io]") {
    auto target = incoming();
    const auto prior = std::make_unique<Records>(*target);
    for (const auto version : std::array<std::uint16_t, 6>{0x25, 0x29, 0x2a, 0x2b, 0x2d, 0xffff}) {
        REQUIRE_THROWS_AS(
            target->read(
                [](std::size_t) -> ReadResult { throw std::logic_error("Unexpected read"); },
                version),
            std::invalid_argument);
        REQUIRE(target->globals == prior->globals);
        REQUIRE(target->civs == prior->civs);
    }
    REQUIRE_THROWS_AS(target->read([](std::size_t) { return ReadResult{{}, true}; }),
                      std::invalid_argument);
    REQUIRE(target->globals == prior->globals);
    REQUIRE_THROWS_AS(
        target->read([](std::size_t size) { return ReadResult{Bytes(size + 1), false}; }, 0x26),
        std::invalid_argument);
    REQUIRE(target->globals.first_discoverer[0] == 0); // Legacy preclear precedes the bad service.
    REQUIRE(target->globals.turn == prior->globals.turn);
    REQUIRE(target->civs[7].units_lost[61] == 0);
}
TEST_CASE("F06 load census rebuilds only active and production counts with byte wrapping",
          "[state][load-census]") {
    auto r = incoming();
    const auto prior = r->civs[1];
    state::EntityPools pools;
    pools.units.set_end(260);
    for (auto& unit : pools.units.scanned()) {
        unit.token = state::UnitToken{1};
        unit.owner = state::CivilizationSlot{1};
        unit.type = state::UnitType{61};
    }
    pools.units.storage()[0].token = state::UnitToken{0};
    pools.units.storage()[0].type = state::UnitType{255};
    pools.units.storage()[1].owner = state::CivilizationSlot{9}; // No matching census owner.
    pools.units.storage()[1].type = state::UnitType{255};
    pools.units.storage()[260].token = state::UnitToken{1}; // Beyond scan bound.
    pools.units.storage()[260].type = state::UnitType{255};
    pools.cities.set_end(256);
    for (auto& city : pools.cities.scanned()) {
        city.token = state::CityToken{1};
        city.owner = state::CivilizationSlot{1};
        city.production = 61;
    }
    state::rebuild_loaded_unit_census(r->civs, pools);
    REQUIRE(r->civs[1].units_active[61] == 2);
    REQUIRE(r->civs[1].units_in_production[61] == 0);
    auto expected = prior;
    expected.units_active.fill(0);
    expected.units_active[61] = 2;
    expected.units_in_production.fill(0);
    REQUIRE(r->civs[1] == expected);
    REQUIRE(r->civs[0].units_active[0] == 0);
    REQUIRE(r->civs[7].units_in_production[61] == 0);
    REQUIRE(r->civs[7].units_lost[61] == 0xa5);
    pools.cities.storage()[0].token = state::CityToken{0};
    pools.cities.storage()[0].production = 127;
    pools.cities.storage()[1].production = -1;
    pools.cities.storage()[2].production = -128;
    state::rebuild_loaded_unit_census(r->civs, pools);
    REQUIRE(r->civs[1].units_in_production[61] == 253);
}
TEST_CASE("F06 load census safe stops preserve earlier civilizations and earlier increments",
          "[state][load-census]") {
    auto r = incoming();
    state::EntityPools pools;
    pools.units.set_end(2);
    for (auto& unit : pools.units.scanned()) {
        unit.token = state::UnitToken{1};
        unit.owner = state::CivilizationSlot{2};
        unit.type = state::UnitType{0};
    }
    pools.units.storage()[1].type = state::UnitType{62};
    REQUIRE_THROWS_AS(state::rebuild_loaded_unit_census(r->civs, pools), std::out_of_range);
    REQUIRE(r->civs[1].units_active[0] == 0);
    REQUIRE(r->civs[2].units_active[0] == 1);
    REQUIRE(r->civs[2].units_in_production[0] == 0);
    REQUIRE(r->civs[3].units_active[0] == 0xa5);
    pools.units.storage()[1].type = state::UnitType{0};
    pools.cities.set_end(2);
    for (auto& city : pools.cities.scanned()) {
        city.token = state::CityToken{1};
        city.owner = state::CivilizationSlot{2};
        city.production = 0;
    }
    pools.cities.storage()[1].production = 62;
    REQUIRE_THROWS_AS(state::rebuild_loaded_unit_census(r->civs, pools), std::out_of_range);
    REQUIRE(r->civs[2].units_active[0] == 2);
    REQUIRE(r->civs[2].units_in_production[0] == 1);
    REQUIRE(r->civs[3].units_active[0] == 0xa5);
}
TEST_CASE("F06 loaded censuses remain stored until the post-renewal rebuild boundary",
          "[formats][world-record-io][load-census]") {
    state::ProcessContext process;
    auto world = std::make_unique<state::GameState>(process);
    auto source = saved();
    source->globals.unit_end = 3;
    source->globals.city_end = 2;
    const auto bytes = joined(items(*source, 0x2c));
    MemoryItems persistent(bytes);
    bool scenario = true;
    REQUIRE(read_persistent_items([&](std::size_t size) { return persistent.read(size); }, 0x2c,
                                  world->globals, world->names, world->civilizations, scenario));
    REQUIRE_FALSE(scenario);
    REQUIRE(world->civilizations[1].units_active[4] == 0x48);
    REQUIRE(world->civilizations[1].units_in_production[6] == 0x6a);
    // The parent establishes entity scan backing at its later input boundary.
    world->entities.units.set_end(static_cast<std::size_t>(world->globals.unit_end));
    world->entities.cities.set_end(static_cast<std::size_t>(world->globals.city_end));
    ByteWriter entity_bytes;
    state::Unit unit;
    unit.token = state::UnitToken{99};
    unit.owner = state::CivilizationSlot{1};
    unit.type = state::UnitType{4};
    entity_bytes.append(encode_unit(unit));
    unit.token = state::UnitToken{0};
    unit.type = state::UnitType{255};
    entity_bytes.append(encode_unit(unit));
    unit.token = state::UnitToken{100};
    unit.type = state::UnitType{5};
    entity_bytes.append(encode_unit(unit));
    state::City city;
    city.token = state::CityToken{55};
    city.owner = state::CivilizationSlot{1};
    city.production = -1;
    entity_bytes.append(encode_city(city));
    city.production = 6;
    entity_bytes.append(encode_city(city));
    process.tokens.next_unit = 0; // Renewal can make an incoming occupied slot a zero-ID hole.
    process.tokens.next_city = 12;
    MemoryItems entities(entity_bytes.bytes);
    REQUIRE(read_entity_items([&](std::size_t size) { return entities.read(size); }, 0x2c,
                              world->entities, process.tokens));
    REQUIRE(process.tokens.next_unit == 2);
    REQUIRE(process.tokens.next_city == 14);
    REQUIRE(world->entities.units.storage()[0].token == state::UnitToken{0});
    REQUIRE(world->civilizations[1].units_active[4] == 0x48);
    state::rebuild_loaded_unit_census(world->civilizations, world->entities);
    REQUIRE(world->civilizations[1].units_active[4] == 0);
    REQUIRE(world->civilizations[1].units_active[5] == 1);
    REQUIRE(world->civilizations[1].units_in_production[6] == 1);
    REQUIRE(world->civilizations[1].units_lost[5] == 0x59);
    REQUIRE(world->civilizations[1].continent_military[0] == 0x1234);
}
