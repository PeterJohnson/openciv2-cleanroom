// Independently authored F06 sections 2/3/5/6 and S01 section 2 cases.
#include "formats/save_aux_io.hpp"
#include "formats/classic_save.hpp"
#include "session/runtime.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>
#include <limits>

using namespace openciv;
using namespace openciv::formats;
namespace {
struct Feed {
    std::vector<Bytes> blocks;
    std::size_t fail = std::numeric_limits<std::size_t>::max(), prefix = 0, calls = 0;
    explicit Feed(std::vector<Bytes> input) : blocks(std::move(input)) {}
    ReadResult read(std::size_t size) {
        REQUIRE(calls < blocks.size());
        const auto& bytes = blocks[calls];
        REQUIRE(size == bytes.size());
        const bool complete = calls++ != fail;
        const auto length = complete ? size : prefix;
        return {{bytes.begin(), bytes.begin() + static_cast<std::ptrdiff_t>(length)}, complete};
    }
};
Bytes pattern(std::size_t size, unsigned seed) {
    Bytes result(size);
    for (std::size_t i = 0; i < size; ++i)
        result[i] = static_cast<Byte>(i * 37 + seed);
    return result;
}
Bytes regions(const world::MapStorage& map) {
    ByteWriter output;
    for (const auto& region : map.regions)
        output.append(encode_region(region));
    return std::move(output.bytes);
}
Bytes power(const state::PowerHistory& history) {
    ByteWriter output;
    for (const auto& row : history)
        output.append(row);
    return std::move(output.bytes);
}
Bytes overlay(Bytes previous, const Bytes& incoming, std::size_t count) {
    std::copy_n(incoming.begin(), count, previous.begin());
    return previous;
}
std::vector<Bytes> layout_input() {
    std::vector<Bytes> blocks{pattern(1200, 17), pattern(106, 43)};
    for (unsigned i = 0; i < 40; ++i)
        blocks.push_back(pattern(2, i * 7 + 128));
    return blocks;
}
Bytes views_bytes(const std::array<state::MapView, 8>& views) {
    ByteWriter output;
    for (const auto& view : views)
        output.append(encode_map_view(view));
    return std::move(output.bytes);
}
} // namespace
TEST_CASE("F06 coarse and region input preserves ordering and unrelated map layers",
          "[formats][save-aux]") {
    world::Tile fallback;
    world::MapStorage map(fallback);
    map.allocate({6, 5}); // Two by two coarse cells, not tile count or floor dimensions.
    map.set_coarse(2, {0, 0}, 99);
    const auto region_bytes = pattern(1024, 31);
    Feed input({{1, 2, 3, 4}, {5, 6, 7, 8}, region_bytes});
    unsigned flushes = 0;
    REQUIRE(read_map_summary_items([&](std::size_t n) { return input.read(n); }, map,
                                   [&] {
                                       ++flushes;
                                       REQUIRE(input.calls == 2);
                                       REQUIRE(map.coarse(1, {1, 1}) == 8);
                                       REQUIRE(map.regions[0] == world::RegionSummary{});
                                   }));
    REQUIRE(flushes == 1);
    REQUIRE(input.calls == 3);
    REQUIRE(regions(map) == region_bytes);
    REQUIRE(map.coarse(0, {0, 1}) == 3);
    REQUIRE(map.coarse(2, {0, 0}) == 99);
    REQUIRE_FALSE(map.coarse_grid(3)[0]);
    REQUIRE(map.tiles()[0].ocean());
}
TEST_CASE("F06 every failed coarse or region item prefix remains published",
          "[formats][save-aux]") {
    const std::vector<Bytes> blocks{{1, 2, 3, 4}, {5, 6, 7, 8}, pattern(1024, 31)};
    for (std::size_t fail = 0; fail < blocks.size(); ++fail)
        for (std::size_t prefix = 0; prefix <= blocks[fail].size(); ++prefix) {
            world::Tile fallback;
            world::MapStorage map(fallback);
            map.allocate({6, 5});
            map.regions[63].land = -123;
            const auto before = regions(map);
            Feed input(blocks);
            input.fail = fail;
            input.prefix = prefix;
            unsigned flushes = 0;
            REQUIRE_FALSE(read_map_summary_items([&](std::size_t n) { return input.read(n); }, map,
                                                 [&] { ++flushes; }));
            REQUIRE(input.calls == fail + 1);
            REQUIRE(flushes == (fail == 2 ? 1U : 0U));
            for (unsigned grid = 0; grid < 2; ++grid)
                for (std::size_t i = 0; i < 4; ++i) {
                    const bool transferred = grid < fail || (grid == fail && i < prefix);
                    REQUIRE(map.coarse_grid(grid)[i] ==
                            (transferred ? std::optional<Byte>{blocks[grid][i]} : std::nullopt));
                }
            REQUIRE(regions(map) == (fail == 2 ? overlay(before, blocks[2], prefix) : before));
        }
}
TEST_CASE("F06 map summary callbacks retain live untouched values and reject replaced backing",
          "[formats][save-aux]") {
    world::Tile fallback;
    world::MapStorage map(fallback);
    map.allocate({6, 5});
    REQUIRE_FALSE(read_map_summary_items(
        [&](std::size_t n) {
            REQUIRE(n == 4);
            map.set_coarse(0, {1, 1}, 91);
            return ReadResult{{7}, false};
        },
        map));
    REQUIRE(map.coarse_grid(0)[0] == 7);
    REQUIRE(map.coarse_grid(0)[3] == 91);
    unsigned calls = 0;
    REQUIRE_FALSE(read_map_summary_items(
        [&](std::size_t n) {
            if (++calls < 3)
                return ReadResult{Bytes(n, 1), true};
            map.regions[63].water = -9;
            return ReadResult{{0xfe}, false};
        },
        map, [&] { map.regions[63].land = 17; }));
    REQUIRE(map.regions[0].land == 254);
    REQUIRE(map.regions[63].land == 17);
    REQUIRE(map.regions[63].water == -9);
    REQUIRE_THROWS_AS(read_map_summary_items(
                          [&](std::size_t n) {
                              map.release();
                              map.allocate({6, 5});
                              return ReadResult{Bytes(n, 2), true};
                          },
                          map),
                      std::out_of_range);
    REQUIRE_FALSE(map.coarse_grid(0)[0]);
    REQUIRE_THROWS_AS(
        read_map_summary_items([](std::size_t n) { return ReadResult{Bytes(n + 1), false}; }, map),
        std::invalid_argument);
}
TEST_CASE("F06 map summary output uses live later blocks and requires produced coarse cells",
          "[formats][save-aux]") {
    world::Tile fallback;
    world::MapStorage map(fallback);
    map.allocate({6, 5});
    for (auto& cell : map.coarse_grid(0))
        cell = 1;
    unsigned calls = 0;
    REQUIRE_THROWS_AS(write_map_summary_items(
                          [&](std::span<const Byte> bytes) {
                              ++calls;
                              REQUIRE(bytes.size() == 4);
                              return true;
                          },
                          map),
                      std::out_of_range);
    REQUIRE(calls == 1);
    for (auto& cell : map.coarse_grid(1))
        cell = 2;
    for (unsigned fail = 0; fail < 4; ++fail) {
        std::vector<Bytes> output;
        REQUIRE(write_map_summary_items(
                    [&](std::span<const Byte> bytes) {
                        output.emplace_back(bytes.begin(), bytes.end());
                        map.set_coarse(1, {0, 0}, 17);
                        map.regions[0].land = 123;
                        return output.size() - 1 != fail;
                    },
                    map) == (fail == 3));
        REQUIRE(output.size() == std::min<std::size_t>(fail + 1, 3));
        REQUIRE(output[0] == Bytes(4, 1));
        if (fail > 0)
            REQUIRE(output[1] == Bytes{17, 2, 2, 2});
        if (fail > 1)
            REQUIRE(decode_region(std::span{output[2]}.first(16)).land == 123);
    }
}
TEST_CASE("F06 layout packing narrows coordinates and unpacking sign extends all rectangles",
          "[formats][save-aux]") {
    state::Layout layout;
    layout.valid = -7;
    for (std::size_t i = 0; i < 13; ++i) {
        const auto k = static_cast<std::int32_t>(i);
        layout.rectangles[i] = {65536 + k, -65537 - k, 32768 + k, -32769 - k};
    }
    SavedLayout packed;
    pack_layout(layout, packed);
    state::Layout decoded;
    unpack_layout(packed, decoded);
    REQUIRE(decoded.valid == -7);
    for (std::size_t i = 0; i < 13; ++i) {
        const auto k = static_cast<std::int32_t>(i);
        REQUIRE(decoded.rectangles[i] == state::Rectangle{k, -1 - k, -32768 + k, 32767 - k});
    }
    const auto bytes = encode_layout(packed);
    REQUIRE(Bytes(bytes.begin() + 2, bytes.begin() + 10) ==
            Bytes{0, 0, 255, 255, 0, 128, 255, 127});
}
TEST_CASE("F06 power layout and forty view fields load in order with a flush before views",
          "[formats][save-aux]") {
    auto blocks = layout_input();
    Feed input(blocks);
    state::PowerHistory history{};
    SavedLayout packed;
    state::Layout layout;
    std::array<state::MapView, 8> views{};
    unsigned flushes = 0;
    REQUIRE(read_history_layout_items(
        [&](std::size_t n) { return input.read(n); }, history, packed, layout, views,
        [&] {
            REQUIRE(input.calls == 2);
            REQUIRE(layout.rectangles[0].left == packed.rectangles[0].left);
            REQUIRE(layout.valid == packed.valid);
            ++flushes;
        }));
    REQUIRE(flushes == 1);
    REQUIRE(input.calls == 42);
    REQUIRE(power(history) == blocks[0]);
    REQUIRE(encode_layout(packed) == blocks[1]);
    ByteWriter expected;
    for (std::size_t i = 2; i < blocks.size(); ++i)
        expected.append(blocks[i]);
    REQUIRE(views_bytes(views) == expected.bytes);
}
TEST_CASE("F06 all failed power layout and view prefixes retain their distinct publications",
          "[formats][save-aux]") {
    const auto blocks = layout_input();
    for (std::size_t fail = 0; fail < blocks.size(); ++fail)
        for (std::size_t prefix = 0; prefix <= blocks[fail].size(); ++prefix) {
            Feed input(blocks);
            input.fail = fail;
            input.prefix = prefix;
            state::PowerHistory history;
            for (auto& row : history)
                row.fill(99);
            SavedLayout packed = decode_layout(pattern(106, 19));
            state::Layout layout;
            layout.rectangles[0].left = 999999;
            const auto old_layout = layout;
            const auto old_packed = encode_layout(packed);
            std::array<state::MapView, 8> views{};
            unsigned flushes = 0;
            REQUIRE_FALSE(read_history_layout_items([&](std::size_t n) { return input.read(n); },
                                                    history, packed, layout, views,
                                                    [&] { ++flushes; }));
            REQUIRE(input.calls == fail + 1);
            REQUIRE(power(history) ==
                    (fail == 0 ? overlay(Bytes(1200, 99), blocks[0], prefix) : blocks[0]));
            REQUIRE(encode_layout(packed) == (fail == 0   ? old_packed
                                              : fail == 1 ? overlay(old_packed, blocks[1], prefix)
                                                          : blocks[1]));
            REQUIRE(flushes == (fail >= 2 ? 1U : 0U));
            if (fail < 2)
                REQUIRE(layout ==
                        old_layout); // Even a full layout transfer reported failed does not unpack.
            else
                REQUIRE(layout.rectangles[0].left == decode_layout(blocks[1]).rectangles[0].left);
            Bytes expected_views(80);
            for (std::size_t i = 2; i <= fail; ++i) {
                const auto length = i == fail ? prefix : blocks[i].size();
                std::copy_n(blocks[i].begin(), length,
                            expected_views.begin() + static_cast<std::ptrdiff_t>(2 * (i - 2)));
            }
            REQUIRE(views_bytes(views) == expected_views);
        }
}
TEST_CASE("F06 layout input callbacks preserve untouched backing and live later fields",
          "[formats][save-aux]") {
    state::PowerHistory history{};
    SavedLayout packed;
    state::Layout layout;
    std::array<state::MapView, 8> views{};
    REQUIRE_FALSE(read_history_layout_items(
        [&](std::size_t) {
            history[149][7] = 87;
            return ReadResult{{5}, false};
        },
        history, packed, layout, views));
    REQUIRE(history[0][0] == 5);
    REQUIRE(history[149][7] == 87);
    unsigned calls = 0;
    REQUIRE_FALSE(read_history_layout_items(
        [&](std::size_t n) {
            if (++calls == 1)
                return ReadResult{Bytes(n), true};
            packed.rectangles[12].bottom = -71;
            layout.valid = 17;
            return ReadResult{{3}, false};
        },
        history, packed, layout, views));
    REQUIRE(packed.valid == 3);
    REQUIRE(packed.rectangles[12].bottom == -71);
    REQUIRE(layout.valid == 17);
    calls = 0;
    REQUIRE_FALSE(read_history_layout_items(
        [&](std::size_t n) {
            if (++calls <= 2)
                return ReadResult{Bytes(n), true};
            REQUIRE(views[0].open == 0x1200);
            views[0].open = 0x3400;
            return ReadResult{{0xfe}, false};
        },
        history, packed, layout, views, [&] { views[0].open = 0x1200; }));
    REQUIRE(views[0].open == 0x34fe);
}
TEST_CASE("F06 history layout output keeps preparation mutations and stops at each failed item",
          "[formats][save-aux]") {
    for (unsigned fail = 0; fail <= 42; ++fail) {
        state::PowerHistory history{};
        history[149][7] = 99;
        SavedLayout packed;
        state::Layout layout;
        layout.rectangles[0].left = 19;
        std::array<state::MapView, 8> views{};
        state::Rectangle screen{10, 20, 1034, 788};
        std::vector<Bytes> output;
        REQUIRE(write_history_layout_items(
                    [&](std::span<const Byte> bytes) {
                        output.emplace_back(bytes.begin(), bytes.end());
                        screen.left = 11; // Power callback changes the subsequent screen copy.
                        views[0].open = -12;
                        views[7].zoom = -7;
                        return output.size() - 1 != fail;
                    },
                    history, screen, packed, layout, views) == (fail == 42));
        REQUIRE(output.size() == std::min<std::size_t>(fail + 1, 42));
        REQUIRE(output[0] == power(history));
        if (fail == 0) {
            REQUIRE(layout.rectangles[0].left == 19);
            REQUIRE(packed == SavedLayout{});
        } else {
            REQUIRE(layout.rectangles[0] == screen);
            REQUIRE(packed.rectangles[0].left == 11);
            REQUIRE(output[1] == encode_layout(packed));
        }
        if (fail >= 2)
            REQUIRE(ByteReader(output[2]).i16() == -12);
        if (fail >= 41)
            REQUIRE(ByteReader(output[41]).i16() == -7);
    }
}
TEST_CASE("F06 completion overrides layout validity using dimensions and captured scenario only",
          "[formats][save-aux]") {
    state::Layout layout;
    layout.rectangles[0] = {10, 20, 1034, 788};
    finish_loaded_layout(layout, {100, -30, 1124, 738}, false);
    REQUIRE(layout.valid == 1); // Equal dimensions, different origin, file marker was zero.
    finish_loaded_layout(layout, {100, -30, 1124, 738}, true);
    REQUIRE(layout.valid == 0);
    finish_loaded_layout(layout, {10, 20, 1035, 788}, false);
    REQUIRE(layout.valid == 0);
    finish_loaded_layout(layout, {10, 20, 1034, 789}, false);
    REQUIRE(layout.valid == 0);
    layout.rectangles[0] = {std::numeric_limits<std::int32_t>::min(), 0,
                            std::numeric_limits<std::int32_t>::max(), 1};
    finish_loaded_layout(layout, {1, 0, 0, 1}, false);
    REQUIRE(layout.valid == 1); // F01 width subtraction wraps to -1.
}
TEST_CASE("S01 parameter reset preserves name tails and F06 gates scenario input independently",
          "[state][save-aux]") {
    for (const bool captured : {false, true})
        for (const auto flags : std::array<std::uint16_t, 3>{0, 0x40, 0x80}) {
            const bool selected = captured || (flags & 0x80) != 0;
            state::GlobalValues globals;
            globals.game_flags = flags;
            auto scenario = decode_scenario_parameters(pattern(100, 9));
            auto expected_reset = scenario;
            expected_reset.flags = 0;
            expected_reset.name.bytes[0] = 0;
            expected_reset.technology_paradigm = 10;
            expected_reset.year_increment = expected_reset.start_year = 0;
            expected_reset.maximum_turns = expected_reset.objective_civ = 0;
            expected_reset.victory_thresholds.fill(0);
            state::EliminationHistory eliminations;
            const auto scenario_input = pattern(100, 11), history_input = pattern(338, 3);
            Feed input(selected ? std::vector<Bytes>{scenario_input, history_input}
                                : std::vector<Bytes>{history_input});
            REQUIRE(read_scenario_history_items(
                [&](std::size_t n) {
                    if (!selected)
                        REQUIRE(scenario == expected_reset); // Reset precedes even the first read.
                    return input.read(n);
                },
                captured, globals, scenario, eliminations));
            REQUIRE(scenario ==
                    (selected ? decode_scenario_parameters(scenario_input) : expected_reset));
            REQUIRE(encode_elimination_history(eliminations) == history_input);
            REQUIRE(globals.game_flags == flags);
        }
}
TEST_CASE("F06 scenario and elimination failures retain each transferred prefix and callbacks",
          "[formats][save-aux]") {
    const std::vector<Bytes> blocks{pattern(100, 7), pattern(338, 11)};
    for (std::size_t fail = 0; fail < 2; ++fail)
        for (std::size_t prefix = 0; prefix <= blocks[fail].size(); ++prefix) {
            state::GlobalValues globals;
            auto scenario = decode_scenario_parameters(pattern(100, 19));
            auto history = decode_elimination_history(pattern(338, 29));
            const auto before_scenario = encode_scenario_parameters(scenario);
            const auto before_history = encode_elimination_history(history);
            Feed input(blocks);
            input.fail = fail;
            input.prefix = prefix;
            REQUIRE_FALSE(read_scenario_history_items([&](std::size_t n) { return input.read(n); },
                                                      true, globals, scenario, history));
            REQUIRE(input.calls == fail + 1);
            REQUIRE(encode_scenario_parameters(scenario) ==
                    (fail == 0 ? overlay(before_scenario, blocks[0], prefix) : blocks[0]));
            REQUIRE(encode_elimination_history(history) ==
                    (fail == 0 ? before_history : overlay(before_history, blocks[1], prefix)));
        }
    state::GlobalValues globals;
    state::ScenarioParameters scenario;
    state::EliminationHistory history;
    REQUIRE_FALSE(read_scenario_history_items(
        [&](std::size_t n) {
            REQUIRE(n == 100);
            scenario.victory_thresholds[3] = -19;
            return ReadResult{{0x80}, false};
        },
        true, globals, scenario, history));
    REQUIRE(scenario.flags == 0x80);
    REQUIRE(scenario.victory_thresholds[3] == -19);
    scenario.name.bytes.fill('X');
    REQUIRE_FALSE(read_scenario_history_items(
        [&](std::size_t n) {
            REQUIRE(n == 338);
            history.names[11].bytes[23] = 91;
            return ReadResult{{0xfe}, false};
        },
        false, globals, scenario, history));
    REQUIRE(history.count == 254);
    REQUIRE(history.names[11].bytes[23] == 91);
    REQUIRE(scenario.name.bytes[0] == 0);
    REQUIRE(scenario.name.bytes[79] == 'X');
    REQUIRE(scenario.technology_paradigm == 10);
}
TEST_CASE("F06 scenario history output uses export or live flag and always writes all history rows",
          "[formats][save-aux]") {
    for (const bool exporting : {false, true})
        for (const auto flags : std::array<std::uint16_t, 3>{0, 0x40, 0x80})
            for (unsigned fail = 0; fail <= 2; ++fail) {
                state::GlobalValues globals;
                globals.game_flags = flags;
                state::ScenarioParameters scenario;
                state::EliminationHistory history;
                history.count = 0;
                history.names[11].bytes[23] = 55;
                const bool selected = exporting || (flags & 0x80) != 0;
                std::vector<Bytes> output;
                REQUIRE(write_scenario_history_items(
                            [&](std::span<const Byte> bytes) {
                                output.emplace_back(bytes.begin(), bytes.end());
                                history.turns[11] = 17;
                                return output.size() - 1 != fail;
                            },
                            exporting, globals, scenario,
                            history) == (fail >= (selected ? 2U : 1U)));
                REQUIRE(output.size() == std::min<std::size_t>(fail + 1, selected ? 2 : 1));
                REQUIRE(output[0].size() == (selected ? 100 : 338));
                if (!selected || output.size() == 2) {
                    const auto decoded = decode_elimination_history(output.back());
                    REQUIRE(decoded.count == 0);
                    REQUIRE(decoded.names[11].bytes[23] == 55);
                    REQUIRE(decoded.turns[11] == (selected ? 17 : 0));
                }
            }
}
TEST_CASE("F06 auxiliary stages retain runtime owned values without changing randomness",
          "[session][save-aux]") {
    state::ProcessContext process;
    session::SessionRuntime runtime(process, session::RuntimeId{52});
    runtime.begin_client_session({1, 2});
    auto& world = runtime.world();
    world.map.allocate({6, 5});
    const auto gameplay = runtime.gameplay_random().checkpoint();
    const auto presentation = runtime.presentation_random().checkpoint();
    Feed map_input({{1, 2, 3, 4}, {5, 6, 7, 8}, pattern(1024, 31)});
    REQUIRE(read_map_summary_items([&](std::size_t n) { return map_input.read(n); }, world.map));
    Feed history_input(layout_input());
    SavedLayout packed;
    REQUIRE(read_history_layout_items([&](std::size_t n) { return history_input.read(n); },
                                      world.power_history, packed, world.layout, world.views));
    Feed scenario_input({pattern(100, 11), pattern(338, 3)});
    REQUIRE(read_scenario_history_items([&](std::size_t n) { return scenario_input.read(n); }, true,
                                        world.globals, world.scenario, world.eliminations));
    REQUIRE(runtime.gameplay_random().checkpoint() == gameplay);
    REQUIRE(runtime.presentation_random().checkpoint() == presentation);
    const auto saved_scenario = world.scenario;
    const auto saved_history = world.eliminations;
    const auto saved_layout = world.layout;
    runtime.end_client_session();
    runtime.begin_client_session({3, 4});
    REQUIRE(world.scenario == saved_scenario);
    REQUIRE(world.eliminations == saved_history);
    REQUIRE(world.layout == saved_layout);
    REQUIRE(power(world.power_history) == history_input.blocks[0]);
    REQUIRE(world.map.coarse(0, {0, 0}) == 1);
}
