#include "formats/world_record_io.hpp"

namespace openciv::formats {
namespace {
template <class Range, class Encode> Bytes encode_array(Range records, Encode encode) {
    ByteWriter writer;
    for (const auto& record : records)
        writer.append(encode(record));
    return std::move(writer.bytes);
}
} // namespace
bool write_persistent_items(const WriteItem& write, const state::GlobalValues& globals,
                            std::span<const state::CivilizationNames, 8> names,
                            std::span<const state::Civilization, 8> civilizations) {
    return write(encode_globals(globals)) &&
           write(encode_array(names, encode_civilization_names)) &&
           write(encode_array(civilizations, encode_civilization));
}
} // namespace openciv::formats
