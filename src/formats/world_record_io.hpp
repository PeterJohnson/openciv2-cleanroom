#pragma once
#include "formats/classic_io.hpp"
#include "formats/save_records.hpp"

namespace openciv::formats {
bool write_persistent_items(const WriteItem& write, const state::GlobalValues& globals,
                            std::span<const state::CivilizationNames, 8> names,
                            std::span<const state::Civilization, 8> civilizations,
                            const RecordCompatibility& compatibility = {});
} // namespace openciv::formats
