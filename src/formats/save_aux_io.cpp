#include "formats/save_aux_io.hpp"
#include "base/arithmetic.hpp"
#include <algorithm>
#include <bit>

namespace openciv::formats {
namespace {
ReadResult item(const ReadItem& read, std::size_t length) {
    auto result = read(length);
    if (result.prefix.size() > length || (result.complete && result.prefix.size() != length))
        throw std::invalid_argument("Item service returned an inconsistent transferred prefix");
    return result;
}
template <class Record, class Encode, class Decode>
bool read_record(const ReadItem& read, Record& record, std::size_t length, Encode encode,
                 Decode decode) {
    const auto incoming = item(read, length);
    auto bytes = encode(record); // Preserve untransferred post-callback state.
    std::copy(incoming.prefix.begin(), incoming.prefix.end(), bytes.begin());
    record = decode(bytes);
    return incoming.complete;
}
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
bool read_map_summary_items(const ReadItem& read, world::MapStorage& map,
                            const FlushLoadWork& flush) {
    for (unsigned grid = 0; grid < 2; ++grid) {
        const auto destination = map.coarse_grid(grid);
        const auto revision = map.storage_revision();
        const auto incoming = item(read, destination.size());
        if (map.storage_revision() != revision)
            throw std::out_of_range("Map callback invalidated the selected coarse grid");
        for (std::size_t i = 0; i < incoming.prefix.size(); ++i)
            destination[i] = incoming.prefix[i];
        if (!incoming.complete)
            return false;
    }
    if (flush)
        flush();
    const auto incoming = item(read, 64 * region_record_size);
    auto bytes = encode_regions(map.regions);
    std::copy(incoming.prefix.begin(), incoming.prefix.end(), bytes.begin());
    ByteReader reader(bytes);
    for (auto& region : map.regions)
        region = decode_region(reader.take(region_record_size));
    return incoming.complete;
}
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
    layout.valid = packed.valid;
    for (std::size_t i = 0; i < layout.rectangles.size(); ++i) {
        const auto& from = packed.rectangles[i];
        auto& to = layout.rectangles[i];
        to.left = from.left;
        to.right = from.right;
        to.top = from.top;
        to.bottom = from.bottom;
    }
}
bool read_history_layout_items(const ReadItem& read, state::PowerHistory& history,
                               SavedLayout& packed, state::Layout& layout,
                               std::span<state::MapView, 8> views, const FlushLoadWork& flush) {
    const auto incoming = item(read, 150 * 8);
    for (std::size_t i = 0; i < incoming.prefix.size(); ++i)
        history[i / 8][i % 8] = incoming.prefix[i];
    if (!incoming.complete)
        return false;
    if (!read_record(read, packed, layout_record_size, encode_layout, decode_layout))
        return false;
    unpack_layout(packed, layout);
    if (flush)
        flush();
    for (auto& view : views)
        for (auto* field : fields(view)) {
            const auto input = item(read, 2);
            auto bytes = word(*field);
            std::copy(input.prefix.begin(), input.prefix.end(), bytes.begin());
            *field = ByteReader(bytes).i16();
            if (!input.complete)
                return false;
        }
    return true;
}
bool write_history_layout_items(const WriteItem& write, const state::PowerHistory& history,
                                const state::Rectangle& current_screen, SavedLayout& packed,
                                state::Layout& layout, std::span<const state::MapView, 8> views) {
    if (!write(encode_power(history)))
        return false;
    layout.rectangles[0] = current_screen;
    pack_layout(layout, packed);
    if (!write(encode_layout(packed)))
        return false;
    for (const auto& view : views)
        for (const auto* field : fields(view))
            if (!write(word(*field)))
                return false;
    return true;
}
void finish_loaded_layout(state::Layout& layout, const state::Rectangle& screen,
                          bool captured_scenario) {
    layout.valid = 1;
    const auto& saved = layout.rectangles[0];
    if (base::subtract32(saved.right, saved.left) != base::subtract32(screen.right, screen.left) ||
        base::subtract32(saved.bottom, saved.top) != base::subtract32(screen.bottom, screen.top) ||
        captured_scenario)
        layout.valid = 0;
}
bool read_scenario_history_items(const ReadItem& read, bool captured_scenario,
                                 const state::GlobalValues& globals,
                                 state::ScenarioParameters& scenario,
                                 state::EliminationHistory& eliminations) {
    if (captured_scenario || (globals.game_flags & 0x80) != 0) {
        if (!read_record(read, scenario, scenario_parameters_size, encode_scenario_parameters,
                         decode_scenario_parameters))
            return false;
    } else {
        state::reset_scenario_parameters(scenario);
    }
    return read_record(read, eliminations, elimination_record_size, encode_elimination_history,
                       decode_elimination_history);
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
