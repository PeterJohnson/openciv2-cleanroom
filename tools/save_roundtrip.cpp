// Native comparison driver for externally supplied original files.
// Also checks isolated ordered auxiliary I/O against the stored record view.
// No complete session load, original execution or semantic validity is claimed.
#include "formats/classic_save.hpp"
#include "formats/save_aux_io.hpp"
#include "platform/native_files.hpp"
#include <iostream>

namespace {
using namespace openciv;
using namespace openciv::formats;
void require(bool condition, const char* message) {
    if (!condition)
        throw std::runtime_error(message);
}
void verify_auxiliary(std::span<const Byte> bytes, const StoredSave& save) {
    constexpr auto map_start =
        12 + global_record_size + 8 * civilization_names_size + 8 * civilization_record_size;
    MemoryItems map_input(bytes.subspan(map_start));
    const ReadItem read_map = [&](std::size_t n) { return map_input.read(n); };
    world::Tile fallback;
    world::MapStorage map(fallback);
    StartingPositions starts;
    require(read_map_items(read_map, MapStream::embedded, map, starts),
            "Embedded map input failed");
    const auto summary_start = map_input.position();
    require(read_map_summary_items(read_map, map), "Map summary input failed");
    require(map.regions == save.map.regions, "Ordered region values differ from stored records");
    ByteWriter summary_output;
    require(write_map_summary_items(
                [&](std::span<const Byte> block) {
                    summary_output.append(block);
                    return true;
                },
                map),
            "Map summary output failed");
    const auto original_summary =
        bytes.subspan(map_start + summary_start, map_input.position() - summary_start);
    require(std::equal(original_summary.begin(), original_summary.end(),
                       summary_output.bytes.begin(), summary_output.bytes.end()),
            "Ordered coarse/region bytes differ from original input");

    const auto tail_start =
        map_start + map_input.position() + save.units.size() * unit_record_size +
        save.cities.size() * city_record_size + 63 + 4 + save.leaders.size() * leader_record_size;
    MemoryItems tail(bytes.subspan(tail_start));
    const ReadItem read_tail = [&](std::size_t n) { return tail.read(n); };
    state::PowerHistory history{};
    SavedLayout packed;
    state::Layout layout;
    std::array<state::MapView, 8> views{};
    state::ScenarioParameters scenario;
    state::EliminationHistory eliminations;
    require(read_history_layout_items(read_tail, history, packed, layout, views),
            "History/layout input failed");
    require(read_scenario_history_items(read_tail, save.scenario.has_value(), save.globals,
                                        scenario, eliminations),
            "Scenario/history input failed");
    require(history == save.power_history && packed == save.layout && views == save.views &&
                (!save.scenario || scenario == *save.scenario) && eliminations == save.eliminations,
            "Ordered auxiliary values differ from stored records");
    ByteWriter tail_output;
    const WriteItem write = [&](std::span<const Byte> block) {
        tail_output.append(block);
        return true;
    };
    require(write_history_layout_items(write, history, layout.rectangles[0], packed, layout, views),
            "History/layout output failed");
    require(write_scenario_history_items(write, save.scenario.has_value(), save.globals, scenario,
                                         eliminations),
            "Scenario/history output failed");
    const auto original_tail = bytes.subspan(tail_start, tail.position());
    require(std::equal(original_tail.begin(), original_tail.end(), tail_output.bytes.begin(),
                       tail_output.bytes.end()),
            "Ordered history/layout/scenario bytes differ from original input");
}
} // namespace
int main(int argc, char** argv) {
    try {
        if (argc != 2 && argc != 3)
            throw std::invalid_argument("Usage: oc2_save_roundtrip INPUT.SAV [NEW_OUTPUT.SAV]");
        const auto bytes = openciv::io::read_file(argv[1]);
        const auto save = openciv::formats::decode_stored_save(bytes);
        const auto encoded = openciv::formats::encode_stored_save(save);
        if (encoded != bytes)
            throw std::runtime_error("Decoded/encoded bytes differ");
        verify_auxiliary(bytes, save);
        if (argc == 3)
            openciv::io::write_new_file(argv[2], encoded);
        std::cout << "{\"version\":" << save.header.version
                  << ",\"width\":" << save.map.header.width
                  << ",\"height\":" << save.map.header.height
                  << ",\"unitSlots\":" << save.units.size()
                  << ",\"citySlots\":" << save.cities.size()
                  << ",\"mode\":" << static_cast<unsigned>(save.globals.multiplayer)
                  << ",\"scenario\":" << (save.scenario ? "true" : "false")
                  << ",\"eventBlock\":" << (save.events ? "true" : "false")
                  << ",\"events\":" << (save.events ? save.events->entries.size() : 0)
                  << ",\"trailingBytes\":" << save.trailing.size() << ",\"byteEqual\":true"
                  << ",\"orderedAuxiliaryEqual\":true"
                  << ",\"outputWritten\":" << (argc == 3 ? "true" : "false") << "}\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
