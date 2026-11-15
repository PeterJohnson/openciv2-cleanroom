#include "formats/state_conversion.hpp"
#include "classic_load_fixture.hpp"
#include "formats/game_input.hpp"
#include "formats/save_aux_io.hpp"
using namespace openciv;
using namespace openciv::formats;
TEST_CASE("F06 layout export snapshots the screen without changing live geometry",
          "[formats][save-aux]") {
    auto stored = load_fixture::saved(true);
    auto input = game_input(stored);
    input.layout.rectangles[1] = {70000, -70000, 80000, -80000};
    const auto before = input.layout;
    SavedLayout packed;
    std::vector<Bytes> blocks;
    REQUIRE(write_history_layout_items(
        [&](std::span<const Byte> bytes) {
            blocks.emplace_back(bytes.begin(), bytes.end());
            return true;
        },
        input.power_history, {0, 0, 1280, 800}, packed, input.layout, input.views));
    CHECK(input.layout == before);
    CHECK(decode_layout(blocks[1]).rectangles[0] == SavedRectangle{0, 0, 1280, 800});
    CHECK(blocks[0].back() == 81);
    REQUIRE_FALSE(write_history_layout_items([](std::span<const Byte>) { return false; },
                                             input.power_history, {}, packed, input.layout,
                                             input.views));
    CHECK(input.layout == before);
}
TEST_CASE("F06 owned restoration input retains map history scenario and layout fields",
          "[formats][save-aux]") {
    const auto original = load_fixture::saved(true);
    const auto input = game_input(decode_stored_save(encode_stored_save(original)));
    CHECK(input.map.regions == original.map.regions);
    CHECK(input.map.coarse == original.map.coarse);
    CHECK(input.power_history == original.power_history);
    for (std::size_t i = 0; i < input.views.size(); ++i)
        CHECK(input.views[i] == to_live(original.views[i]));
    REQUIRE(input.scenario);
    REQUIRE(original.scenario);
    CHECK(*input.scenario == to_live(*original.scenario));
    CHECK(input.eliminations == to_live(original.eliminations));
}
