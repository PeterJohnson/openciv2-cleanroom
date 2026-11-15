#include <catch2/catch_test_macros.hpp>

import std;
import openciv.state.records;

using namespace openciv::state;

TEST_CASE("F02 stable pools distinguish scan bounds occupancy and retained payload",
          "[state][F02]") {
    // F02 §§3,10: [11,0,13,0,15] -> [11,0,13,0,0], end 5 -> 4.
    UnitPool pool;
    pool.set_end(5);
    const std::array values{11U, 0U, 13U, 0U, 15U};
    for (std::size_t i = 0; i < values.size(); ++i) {
        pool.storage()[i].token = UnitToken{values[i]};
        pool.storage()[i].damage = 37;
    }
    auto* record = &pool.at(UnitSlot{4});
    CHECK(pool.finish_deletion(4));
    CHECK(pool.end() == 4);
    CHECK(pool.storage()[4].token == UnitToken{0});
    CHECK(pool.storage()[4].damage == 37);
    CHECK_FALSE(pool.finish_deletion(3));
    CHECK(pool.end() == 4); // no trimming earlier holes
    CHECK_FALSE(pool.finish_deletion(-1));
    CHECK_THROWS_AS(pool.finish_deletion(4), std::out_of_range);
    CHECK_THROWS_AS(pool.set_end(2049), std::out_of_range);
    CHECK(pool.end() == 4);
    pool.storage()[100].token = UnitToken{91};
    CHECK_FALSE(pool.live(UnitSlot{100}));
    pool.set_end(101);
    CHECK(pool.live(UnitSlot{100}));
    CHECK(&pool.at(UnitSlot{4}) == record);
    CHECK_THROWS_AS(pool.at(UnitSlot{2048}), std::out_of_range);
    CHECK_FALSE(pool.live(UnitSlot{65535}));
    CHECK(std::as_const(pool).scanned().size() == 101);
}

TEST_CASE("F02 owning-pool reservation preserves payload and delegates limits", "[state][F02]") {
    UnitPool pool;
    pool.set_end(2040);
    for (auto& unit : pool.scanned())
        unit.token = UnitToken{1};
    pool.storage()[7].token = UnitToken{0};
    pool.storage()[7].damage = 10;
    pool.storage()[7].orders = UnitOrder::sleeping;
    CHECK(reserve_unit_slot(pool, CivilizationSlot{2}, 2000, 0) == UnitSlot{7});
    CHECK(pool.end() == 2040);
    CHECK(pool.storage()[7].damage == 10);
    CHECK(pool.storage()[7].orders == UnitOrder::sleeping);
    CHECK(pool.storage()[7].token == UnitToken{0});
    pool.storage()[7].token = UnitToken{1};
    CHECK_FALSE(reserve_unit_slot(pool, CivilizationSlot{2}, 1948, 0));
    CHECK(reserve_unit_slot(pool, CivilizationSlot{2}, 1948, 4) == UnitSlot{2040});
    CHECK(pool.end() == 2041);
}

TEST_CASE("F02 and F05 distinguish process new-session and inner reset lifetimes",
          "[state][F02][F05]") {
    // F05 §2 vs §5: invalidation clears tokens/links; inner reset only bounds/selection.
    EntityPools pools;
    ProcessTokens process{100, 200};
    pools.units.set_end(2);
    pools.cities.set_end(1);
    auto& unit = pools.units.at(UnitSlot{1});
    unit.token = process.assign_unit();
    unit.previous.value = 0;
    unit.next.value = 2;
    unit.damage = 77;
    unit.destination_x = 32;
    unit.orders = UnitOrder::sleeping;
    pools.units.at(UnitSlot{2047}) = unit;
    auto& city = pools.cities.at(CitySlot{255});
    city.token = process.assign_city();
    city.size = -128;
    city.food_box = -32768;
    std::optional<UnitSlot> selected;
    reset_entity_scans(pools, selected);
    CHECK(pools.units.end() == 0);
    CHECK(pools.cities.end() == 0);
    CHECK(selected == UnitSlot{0});
    CHECK_FALSE(pools.units.live(*selected));
    CHECK(unit.token == UnitToken{100});
    CHECK(unit.previous.value == 0);
    invalidate_entities_for_new_session(pools);
    for (const auto& record : pools.units.storage()) {
        CHECK(record.token == UnitToken{0});
        CHECK(record.previous.value == -1);
        CHECK(record.next.value == -1);
    }
    CHECK(unit.damage == 77);
    CHECK(unit.destination_x == 32);
    CHECK(unit.orders == UnitOrder::sleeping);
    CHECK(pools.units.at(UnitSlot{2047}).damage == 77);
    CHECK(city.token == CityToken{0});
    CHECK(city.size == -128);
    CHECK(city.food_box == -32768);
    CHECK(process == ProcessTokens{101, 201});
    ProcessTokens independent_peer;
    CHECK(independent_peer == ProcessTokens{1, 1});
}

TEST_CASE("F02 lifetime renewal preserves holes references and numeric wrap", "[state][F02][F06]") {
    EntityPools pools;
    pools.units.set_end(3);
    pools.cities.set_end(2);
    pools.units.storage()[0].token = UnitToken{50};
    pools.units.storage()[2].token = UnitToken{90};
    pools.units.storage()[0].home = CitySlot{200};
    pools.units.storage()[0].next.value = 2;
    pools.units.storage()[1].damage = 123;
    pools.cities.storage()[0].token = CityToken{7};
    pools.cities.storage()[1].token = CityToken{9};
    ProcessTokens process{100, 200};
    renew_loaded_lifetimes(pools, process, false);
    CHECK(pools.units.storage()[0].token == UnitToken{100});
    CHECK(pools.units.storage()[1].token == UnitToken{0});
    CHECK(pools.units.storage()[2].token == UnitToken{101});
    CHECK(pools.units.storage()[0].home == CitySlot{200});
    CHECK(pools.units.storage()[0].next.slot() == UnitSlot{2});
    CHECK(pools.units.storage()[1].damage == 123);
    CHECK(pools.cities.storage()[0].token == CityToken{200});
    CHECK(pools.cities.storage()[1].token == CityToken{201});
    CHECK(process == ProcessTokens{102, 202});
    process.next_unit = 0xffffffff;
    renew_loaded_lifetimes(pools, process, false);
    CHECK(pools.units.storage()[0].token == UnitToken{0xffffffff});
    CHECK(pools.units.storage()[2].token == UnitToken{0});
    CHECK(process.next_unit == 1);
    renew_loaded_lifetimes(pools, process, true);
    CHECK(pools.units.storage()[1].token == UnitToken{2}); // legacy holes receive IDs too
    CHECK(pools.units.storage()[2].token == UnitToken{3});
}

TEST_CASE("F02 cached city calculation reads the shared workspace without selecting a city",
          "[state][F02]") {
    // F02 §9: cached result is not keyed by city, absent is distinct from slot 0.
    CityMoodWorkspace workspace{CitySlot{4}, 5, 2};
    const auto before = workspace;
    CHECK(city_calculation_entry(CitySlot{9}, false, workspace) == 3);
    CHECK(city_calculation_entry(std::nullopt, true, workspace, [] {
              FAIL("No full call for absent city");
              return 0;
          }) == 0);
    CHECK(workspace == before);
    CHECK(city_calculation_entry(CitySlot{0}, true, workspace, [&] {
              CHECK(workspace.city == CitySlot{0});
              workspace.happy = 8;
              return 6;
          }) == 6);
    CHECK(workspace.happy == 8);
    workspace.happy = std::numeric_limits<std::int32_t>::max();
    workspace.unhappy = -1;
    CHECK(city_calculation_entry(CitySlot{9}, false, workspace) ==
          std::numeric_limits<std::int32_t>::min());
    CHECK_THROWS_AS(city_calculation_entry(CitySlot{5}, true, workspace,
                                           [&]() -> std::int32_t {
                                               workspace.happy = 17;
                                               throw std::runtime_error("scripted child failure");
                                           }),
                    std::runtime_error);
    CHECK(workspace.city == CitySlot{5});
    CHECK(workspace.happy == 17);
}
