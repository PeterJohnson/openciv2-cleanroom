#include <catch2/catch_test_macros.hpp>

import std;
import openciv.world.map;

using namespace openciv::world;

TEST_CASE("W01 tile lookup aliases parity while scans retain canonical order", "[world][W01]") {
    // W01 §§1–2 and §9: lookup is rectangular; no wrapping or parity repair.
    const Geometry geometry{100, 80};
    CHECK(geometry.tile_index({8, 5}) == 254);
    CHECK(geometry.tile_index({9, 5}) == 254);
    CHECK_FALSE(geometry.canonical({8, 5}));
    CHECK(geometry.canonical({9, 5}));
    CHECK_FALSE(geometry.tile_index({100, 0}));
    CHECK_FALSE(geometry.tile_index({-1, 0}));
    CHECK_FALSE(geometry.tile_index({0, 80}));
    std::size_t index = 0;
    for (std::int32_t y = 0; y < 80; ++y) {
        for (std::int32_t x = y & 1; x < 100; x += 2) {
            CHECK(geometry.coordinates(index) == Position{x, y});
            CHECK(geometry.tile_index({x, y}) == index++);
        }
    }
    CHECK(index == geometry.area());
    CHECK_THROWS_AS(geometry.coordinates(index), std::out_of_range);
    CHECK_THROWS_AS((Geometry{3, 4}), std::invalid_argument);
    CHECK_THROWS_AS((Geometry{0, 4}), std::invalid_argument);
    CHECK_THROWS_AS((Geometry{8, -1}), std::invalid_argument);
    CHECK_THROWS_AS((Geometry{256, 256}), std::invalid_argument);
    CHECK(Geometry{2, 32767}.area() == 32767);
}

TEST_CASE("W01 wrapping and distances use current topology and signed reductions", "[world][W01]") {
    // W01 §§2,4,9 / Appendix A: exactly one correction, never modulo.
    const Geometry geometry{100, 80};
    const std::array inputs{-1, 100, 201, -201};
    const std::array outputs{99, 0, 101, -101};
    for (std::size_t i = 0; i < inputs.size(); ++i) {
        CHECK(geometry.wrap_x(inputs[i], false) == outputs[i]);
        CHECK(geometry.wrap_x(inputs[i], true) == inputs[i]);
    }
    const Geometry odd_coarse{10, 7};
    CHECK(odd_coarse.coarse_width() == 3);
    CHECK(odd_coarse.coarse_height() == 2);
    CHECK(odd_coarse.coarse_wrap_x(-1, false) == 2);
    CHECK(odd_coarse.coarse_wrap_x(7, false) == 4);
    CHECK(odd_coarse.coarse_wrap_x(7, true) == 7);
    CHECK(geometry.wrapped_dx(98, 0, false) == 2);
    CHECK(geometry.step_distance({98, 0}, {0, 2}, false) == 2);
    CHECK(geometry.weighted_distance({98, 0}, {0, 2}, false) == 2);
    CHECK(geometry.step_distance({98, 0}, {0, 2}, true) == 50);
    CHECK(geometry.wrapped_dx(0, 50, false) == 50);
    CHECK(weighted_from_deltas({6, 2}) == 5);
    CHECK(weighted_from_deltas({2, 6}) == 5);
    CHECK(geometry.step_distance({0, 0}, {6, 2}, true) == 4);
    CHECK(geometry.wrapped_dx(0, 250, false) == -150);
    CHECK(geometry.step_distance({0, 0}, {250, 0}, false) == -75);
    CHECK(geometry.weighted_distance({0, 0}, {250, 0}, false) == 112);
    constexpr auto minimum = std::numeric_limits<std::int32_t>::min();
    constexpr auto maximum = std::numeric_limits<std::int32_t>::max();
    CHECK(geometry.wrapped_dx(minimum, 0, false) == minimum);
    CHECK(geometry.step_distance({minimum, 0}, {0, 0}, true) == -1073741824);
    CHECK(geometry.step_distance({minimum, minimum}, {0, 0}, true) == 0);
    CHECK(weighted_from_deltas({minimum, 0}) == -536870912);
    // Sum wraps to -2, h=-1, correction=-1073741824; final subtraction wraps.
    CHECK(weighted_from_deltas({maximum, maximum}) == -1073741825);
    CHECK(geometry.wrapped_dx(maximum, -1, true) == minimum);
}

TEST_CASE("W01 neighborhoods retain input order duplicates and supplied coordinates",
          "[world][W01]") {
    // W01 §§3–4: center is last; altered direction tables use the last match.
    NeighborhoodTables tables;
    CHECK(tables.radius[20] == Position{0, 0});
    CHECK(tables.radius[25] == Position{1, -5});
    CHECK(tables.radius[44] == Position{-1, -5});
    CHECK(direction_of({20, -1}, tables) == 0);
    CHECK(direction_of({0, 0}, tables) == 8);
    tables.directions[7] = {50, -2};
    CHECK(direction_of({20, -1}, tables) == 7);
    tables.directions[6] = {0, 0};
    CHECK(direction_of({0, 0}, tables) == 6);
    const Geometry geometry{4, 4};
    const auto diagonal = geometry.neighbors({0, 0}, tables.diagonals, false);
    CHECK(diagonal == std::vector<Position>{{1, 1}, {3, 1}, {0, 0}});
    const std::array<Position, 4> altered{{{1, 0}, {1, 0}, {9, 0}, {-1, 0}}};
    CHECK(geometry.neighbors({0, 0}, altered, false) ==
          std::vector<Position>{{1, 0}, {1, 0}, {3, 0}});
    CHECK(geometry.neighbors({0, 0}, altered, true) == std::vector<Position>{{1, 0}, {1, 0}});
}

TEST_CASE("W01 spans and rectangles retain half-open endpoints", "[world][W01]") {
    // W01 §4: the second cylindrical correction observes the first one.
    const Geometry geometry{100, 80};
    for (auto x : {0, 4, 95})
        CHECK(geometry.contains_x(x, 95, 10, false));
    CHECK_FALSE(geometry.contains_x(5, 95, 10, false));
    CHECK_FALSE(geometry.contains_x(0, 95, 10, true));
    CHECK_FALSE(geometry.contains_x(94, 95, 10, false));
    CHECK_FALSE(geometry.contains_x(-201, 95, 10, false));
    CHECK(geometry.contains_rectangle({0, 9}, {95, 7}, {10, 3}, false));
    CHECK_FALSE(geometry.contains_rectangle({0, 10}, {95, 7}, {10, 3}, false));
    CHECK(geometry.contains_rectangle({2, 99}, {0, 90}, {10, 10}, true));
    CHECK_FALSE(geometry.contains_x(95, 95, 0, false));
    CHECK_FALSE(geometry.contains_rectangle({0, 0}, {0, 0}, {10, -1}, true));
}

TEST_CASE("W01 allocation and release preserve separately owned spatial state", "[world][W01]") {
    // W01 §2: allocation is not new-world initialization of retained state.
    Tile fallback;
    fallback.owner = 3;
    fallback.fertility = 7;
    MapStorage map{fallback};
    map.pattern_seed = 24;
    map.stored_topology = 1;
    map.regions[5] = {2, 3, 4, 8, {9, 10}};
    map.remembered_barbarian = {42};
    map.allocate(Geometry{8, 4});
    for (unsigned civ = 1; civ < 8; ++civ) {
        CHECK(std::ranges::all_of(map.remembered_layer(civ), [](auto v) { return v == 0; }));
    }
    for (unsigned grid = 0; grid < 4; ++grid) {
        CHECK_THROWS_AS(map.coarse(grid, {0, 0}), std::logic_error);
    }
    REQUIRE(map.allocated());
    CHECK(std::ranges::all_of(map.tiles(), [](const auto& tile) { return tile == Tile{}; }));
    CHECK(map.tile({0, 0}).owner == 0);
    CHECK(&map.tile({-1, 0}) == &map.tile({100, 50}));
    map.mutate({TileField::fertility, {-1, 0}, -1});
    CHECK(map.tile({100, 50}).fertility == 15);
    CHECK(fallback.owner == 3);
    CHECK_FALSE(map.owner({-1, 0}));
    map.tile({2, 1}).region = 9;
    CHECK(map.tile({3, 1}).region == 9);
    map.release();
    CHECK_FALSE(map.allocated());
    CHECK(map.geometry().area() == 16);
    CHECK(map.pattern_seed == 24);
    CHECK(map.stored_topology == 1);
    CHECK(map.regions[5] == RegionSummary{2, 3, 4, 8, {9, 10}});
    CHECK(map.remembered(0, {0, 0}) == 42);
    CHECK(map.tile({100, 50}).fertility == 15);
    CHECK_THROWS_AS(map.tile({0, 0}), std::logic_error);
    map.release();
    map.allocate(Geometry{4, 2});
    CHECK(fallback.fertility == 15);
    CHECK(map.pattern_seed == 24);
}

TEST_CASE("W01 remembered and coarse layers use backed direct indices", "[world][W01]") {
    // W01 §§1–2,5: no fallback, wrapping, rectangular test or invented zero.
    Tile fallback;
    MapStorage map{fallback};
    map.allocate(Geometry{8, 8});
    map.remembered(1, {6, 0}) = 55;
    CHECK(map.remembered(1, {-1, 1}) == 55); // (-1 >> 1) + 4 = 3
    CHECK(std::as_const(map).remembered(1, {7, 0}) == 55);
    CHECK_THROWS_AS(map.remembered(1, {-1, 0}), std::out_of_range);
    CHECK_THROWS_AS(map.remembered(1, {0, 8}), std::out_of_range);
    CHECK_THROWS_AS(map.remembered(0, {0, 0}), std::out_of_range);
    CHECK_THROWS_AS(map.remembered_layer(8), std::out_of_range);
    map.set_coarse(2, {1, 0}, 73);
    CHECK(map.coarse(2, {-1, 1}) == 73);
    CHECK_THROWS_AS(map.coarse(1, {1, 0}), std::logic_error);
    CHECK_THROWS_AS(map.set_coarse(2, {-1, 0}, 9), std::out_of_range);
    CHECK_THROWS_AS(map.set_coarse(4, {0, 0}, 9), std::out_of_range);
}

TEST_CASE("W01 spatial reads use retained fields and compound feature gates", "[world][W01]") {
    // W01 §§5,9: no census or repair of retained owner/region/memory values.
    Tile fallback;
    MapStorage map{fallback};
    map.allocate(Geometry{8, 4});
    auto& tile = map.tile({0, 0});
    tile.owner = 3;
    tile.features = 0x42;
    CHECK(map.airbase_owner({0, 0}) == 3);
    CHECK_FALSE(map.city_owner({0, 0}));
    CHECK_FALSE(map.unit_owner({0, 0}));
    CHECK_FALSE(map.city_or_unit_owner({0, 0}));
    map.mutate({TileField::features, {0, 0}, 0x40, 0});
    CHECK(map.city_owner({0, 0}) == 3);
    CHECK(map.city_or_unit_owner({0, 0}) == 3);
    for (const auto fertility : {0, 1, 8, 9, 15}) {
        tile.fertility = static_cast<std::uint8_t>(fertility);
        CHECK(map.claimant_or_fertility({0, 0}) ==
              (fertility >= 1 && fertility <= 8 ? 8 : fertility));
    }
    tile.claimant = 3;
    CHECK(map.claimant_or_fertility({0, 0}) == 3);
    tile.region = 7;
    CHECK_FALSE(map.land_region({0, 0}));
    tile.terrain = Terrain::desert;
    CHECK(map.land_region({0, 0}) == 7);
    tile.revelation = 0x89;
    CHECK(map.revelation({0, 0}, -1) == 1);
    CHECK(map.revelation({0, 0}, 3) == 8);
    CHECK(map.revelation({0, 0}, 7) == 128);
    CHECK(map.revelation({0, 0}, 8) == 0);
    CHECK(map.revelation({0, 0}, 32) == 1);
    Position selected{7, 3};
    CHECK(map.select_terrain({0, 0}, true, true, selected));
    CHECK(selected == Position{1, 1});
    CHECK_FALSE(map.select_terrain({30, 4}, false, false, selected));
    CHECK(selected == Position{1, 1});
    CHECK(map.select_terrain({8, 0}, false, false, selected));
    CHECK(selected == Position{0, 0});
}

TEST_CASE("W01 coordinate features use original coordinates and saved seed", "[world][W01]") {
    // W01 §§8–9: resource floor division, suppression and ownership are distinct.
    Tile fallback;
    MapStorage map{fallback};
    map.allocate(Geometry{8, 8});
    auto& tile = map.tile({0, 0});
    tile.terrain = Terrain::desert;
    tile.owner.reset();
    map.pattern_seed = 16;
    CHECK(map.special_resource({0, 0}) == 2);
    CHECK_FALSE(map.hut({0, 0}));
    CHECK(map.special_resource({1, 0}) == 0); // same tile, different pattern
    map.pattern_seed = 24;
    CHECK(map.hut({0, 0}));
    tile.terrain_flags = 0x40;
    tile.features = 0x01; // retained unit bit does not gate the hut
    CHECK(map.hut({0, 0}));
    CHECK(map.special_resource({0, 0}) == 0);
    tile.owner = 0;
    CHECK_FALSE(map.hut({0, 0}));
    // (0,2): u=1,v=-1,cell=13,pattern=-13+26=13. Truncation would fail.
    map.pattern_seed = 26;
    CHECK(map.special_resource({0, 2}) == 1); // Ocean can have a resource.
    map.tile({0, 2}).terrain = Terrain::grassland;
    CHECK(map.special_resource({0, 2}) == 0);
    CHECK(map.special_resource({8, 0}) == 0);
    CHECK_FALSE(map.hut({8, 0}));
    CHECK(map.pattern_seed == 26);
    CHECK(grassland_shield({0, 0}));
    CHECK(grassland_shield({2, 0}));
    CHECK_FALSE(grassland_shield({1, 1}));
    CHECK(grassland_shield({std::numeric_limits<std::int32_t>::min(), 0}));
}

TEST_CASE("W01 tile edits reduce fields and retain unrelated spatial state", "[world][W01]") {
    // W01 §6 / Appendix A: primitive edits never rebuild derived values.
    Tile fallback;
    MapStorage map{fallback};
    map.allocate(Geometry{4, 2});
    auto& tile = map.tile({0, 0});
    tile.terrain = Terrain::hills;
    tile.terrain_flags = 0x80;
    tile.region = 7;
    tile.radius_other = 31;
    map.regions[7].land = 10;
    map.remembered(1, {0, 0}) = 99;
    map.set_coarse(0, {0, 0}, 9);
    map.mutate({TileField::terrain, {0, 0}, 64});
    CHECK(tile.terrain == Terrain::desert);
    CHECK(tile.terrain_flags == 0xc0);
    map.mutate({TileField::terrain, {0, 0}, 10});
    CHECK(tile.ocean());
    CHECK(tile.terrain_flags == 0xc0);
    CHECK(tile.region == 7);
    CHECK(map.regions[7].land == 10);
    CHECK(map.remembered(1, {0, 0}) == 99);
    CHECK(map.coarse(0, {0, 0}) == 9);
    map.mutate({TileField::fertility, {0, 0}, -1});
    map.mutate({TileField::region, {0, 0}, -1});
    map.mutate({TileField::claimant, {0, 0}, -1});
    CHECK(tile.fertility == 15);
    CHECK(tile.owner == 0);
    CHECK(tile.region == 255);
    CHECK(tile.claimant == 7);
    CHECK(tile.radius_other == 31);
    map.mutate({TileField::owner, {0, 0}, 8});
    CHECK(map.owner({0, 0}) == 8);
    map.mutate({TileField::owner, {0, 0}, 9});
    CHECK_FALSE(map.owner({0, 0}));
    map.mutate({TileField::owner, {0, 0}, -1});
    CHECK_FALSE(map.owner({0, 0}));
    CHECK(tile.fertility == 15);
    map.mutate({TileField::revelation, {0, 0}, -1, -1});
    map.mutate({TileField::revelation, {0, 0}, 0x180, 0});
    CHECK(tile.revelation == 0x7f);
    map.mutate({TileField::features, {0, 0}, 0x120});
    CHECK(tile.features == 0x20); // railroad does not imply a road bit
}

TEST_CASE("W01 tile synchronization publishes after mutation with original arguments",
          "[world][W01]") {
    // W01 §6: client batch wins; callback failure retains local mutation.
    Tile fallback;
    MapStorage map{fallback};
    map.allocate(Geometry{4, 2});
    std::vector<TileSyncEffect> effects;
    const TilePublisher publish = [&](const auto& effect) {
        CHECK(map.tile(effect.mutation.position).features == 1);
        effects.push_back(effect);
    };
    TileMutation mutation{TileField::features, {1, 0}, 257, -2, true};
    map.mutate(mutation, {3, true, true, false}, publish);
    REQUIRE(effects.size() == 1);
    CHECK(effects[0] == TileSyncEffect{TileSyncRoute::client_batch, mutation});
    map.mutate(mutation, {3, true, true, true}, publish);
    CHECK(effects.size() == 1);
    for (const auto controls :
         {TileSyncControls{3, false, true, true}, TileSyncControls{3, true, false, true}}) {
        map.tile({0, 0}).features = 0;
        map.mutate(mutation, controls, publish);
    }
    REQUIRE(effects.size() == 3);
    CHECK(effects[1].route == TileSyncRoute::broadcast);
    CHECK(effects[2].route == TileSyncRoute::request_host);
    for (const auto controls :
         {TileSyncControls{2, true, true, true}, TileSyncControls{3, false, true, false}}) {
        map.tile({0, 0}).features = 0;
        map.mutate(mutation, controls, publish);
    }
    CHECK(effects.size() == 3);
    map.mutate({TileField::owner, {-1, 0}, 9, 1, true}, {3, false, false, true},
               [&](const auto& effect) {
                   CHECK_FALSE(fallback.owner);
                   CHECK(effect.mutation.operand == 15);
                   CHECK(effect.mutation.position == Position{-1, 0});
               });
    REQUIRE_THROWS_AS(map.mutate({TileField::fertility, {0, 0}, -1, 1, true},
                                 {3, true, false, true},
                                 [](const auto& effect) {
                                     CHECK(effect.mutation.operand == -1);
                                     throw std::runtime_error("scripted publisher failure");
                                 }),
                      std::runtime_error);
    CHECK(map.tile({0, 0}).fertility == 15);
}
