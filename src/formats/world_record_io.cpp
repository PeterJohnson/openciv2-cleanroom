#include "formats/world_record_io.hpp"
#include "formats/state_conversion.hpp"

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
                            std::span<const state::Civilization, 8> civilizations,
                            const RecordCompatibility& compatibility) {
    auto record = to_stored(globals);
    preserve_opaque(record, compatibility.globals);
    std::size_t index = 0;
    return write(encode_globals(record)) &&
           write(encode_array(
               names, [](const auto& value) { return encode_civilization_names(value); })) &&
           write(encode_array(civilizations, [&](const auto& value) {
               auto stored = to_stored(value);
               preserve_opaque(stored, compatibility.civilizations[index++]);
               return encode_civilization(stored);
           }));
}
} // namespace openciv::formats
