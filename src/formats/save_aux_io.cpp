#include "formats/save_aux_io.hpp"
#include "formats/state_conversion.hpp"
#include "base/arithmetic.hpp"
#include <algorithm>
#include <bit>

namespace openciv::formats {
namespace {
Bytes encode_regions(const std::array<world::RegionSummary, 64>& regions) {
    ByteWriter writer;
    for (const auto& region : regions)
        writer.append(encode_region(region));
    return std::move(writer.bytes);
}
Bytes encode_power(const state::PowerHistory& history) {
    ByteWriter writer;
    for (const auto& sample : history)
        writer.append(sample);
    return std::move(writer.bytes);
}
std::int16_t narrow(std::int32_t value) {
    return std::bit_cast<std::int16_t>(static_cast<std::uint16_t>(value));
}
template <class View> auto fields(View& view) {
    return std::array{&view.open, &view.track_moves, &view.x, &view.y, &view.zoom};
}
Bytes word(std::int16_t value) {
    ByteWriter writer;
    writer.i16(value);
    return std::move(writer.bytes);
}
} // namespace
bool write_map_summary_items(const WriteItem& write, const world::MapStorage& map) {
    for (unsigned grid = 0; grid < 2; ++grid) {
        Bytes bytes;
        for (const auto& cell : map.coarse_grid(grid)) {
            if (!cell)
                throw std::out_of_range("Coarse grid output requires initialized cells");
            bytes.push_back(*cell);
        }
        if (!write(bytes))
            return false;
    }
    return write(encode_regions(map.regions));
}
void pack_layout(const state::Layout& layout, SavedLayout& packed) {
    packed.valid = layout.valid;
    for (std::size_t i = 0; i < layout.rectangles.size(); ++i) {
        const auto& from = layout.rectangles[i];
        auto& to = packed.rectangles[i];
        to.left = narrow(from.left);
        to.right = narrow(from.right);
        to.top = narrow(from.top);
        to.bottom = narrow(from.bottom);
    }
}
void unpack_layout(const SavedLayout& packed, state::Layout& layout) {
    layout.valid = packed.valid != 0;
    for (std::size_t i = 0; i < layout.rectangles.size(); ++i) {
        const auto& from = packed.rectangles[i];
        auto& to = layout.rectangles[i];
        to.left = from.left;
        to.right = from.right;
        to.top = from.top;
        to.bottom = from.bottom;
    }
}
bool write_history_layout_items(const WriteItem& write, const state::PowerHistory& history,
                                const state::Rectangle& current_screen, SavedLayout& packed,
                                const state::Layout& layout,
                                std::span<const state::MapView, 8> views) {
    if (!write(encode_power(history)))
        return false;
    auto snapshot = layout;
    snapshot.rectangles[0] = current_screen;
    pack_layout(snapshot, packed);
    if (!write(encode_layout(packed)))
        return false;
    for (const auto& view : views) {
        auto stored = to_stored(view);
        for (const auto* field : fields(stored))
            if (!write(word(*field)))
                return false;
    }
    return true;
}
bool write_scenario_history_items(const WriteItem& write, bool scenario_export,
                                  const state::GlobalValues& globals,
                                  const state::ScenarioParameters& scenario,
                                  const state::EliminationHistory& eliminations) {
    if ((scenario_export || (globals.game_flags & 0x80) != 0) &&
        !write(encode_scenario_parameters(scenario)))
        return false;
    return write(encode_elimination_history(eliminations));
}
} // namespace openciv::formats
