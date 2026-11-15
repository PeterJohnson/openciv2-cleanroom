export module openciv.formats.world_record_io;

import std;
export import openciv.formats.classic_io;
export import openciv.formats.save_records;

export extern "C++" {
    namespace openciv::formats {
    bool write_persistent_items(const WriteItem& write, const state::GlobalValues& globals,
                                std::span<const state::CivilizationNames, 8> names,
                                std::span<const state::Civilization, 8> civilizations,
                                const RecordCompatibility& compatibility = {});
    } // namespace openciv::formats
}
