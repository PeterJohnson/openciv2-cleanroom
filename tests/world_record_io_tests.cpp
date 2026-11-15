#include "state/game.hpp"
#include <catch2/catch_test_macros.hpp>
using namespace openciv;
#include "formats/save_records.hpp"
namespace {
struct Records {
    std::array<state::Civilization, 8> civs;
};
std::unique_ptr<Records> incoming() {
    auto result = std::make_unique<Records>();
    for (auto& civ : result->civs)
        civ = formats::decode_civilization(formats::Bytes(formats::civilization_record_size, 0xa5));
    return result;
}
} // namespace
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
