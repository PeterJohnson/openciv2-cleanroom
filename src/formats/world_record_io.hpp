#pragma once

#include "formats/classic_io.hpp"
#include "formats/save_records.hpp"

namespace openciv::formats {
// F06 section 6 stages 5-6 / section 7. The parent has already checked the
// header and installed rules. Supports core versions 26, 27, 28 and 2c.
// Each read publishes its transferred prefix over live record bytes; legacy
// preclears and earlier effects survive failure. Enemy count changes only
// after the entire game block succeeds, before reading name overrides.
// The scenario output changes only after all persistent items succeed.
// Does not allocate a map, change pool scan bounds, renew IDs or admit a session.
bool read_persistent_items(const ReadItem& read, std::uint16_t version,
                           state::GlobalValues& globals,
                           std::span<state::CivilizationNames, 8> names,
                           std::span<state::Civilization, 8> civilizations, bool& scenario);
// F06 section 5 block order: current globals, names, civilizations as three
// separate items. Parent owns save preparation, version and later blocks.
bool write_persistent_items(const WriteItem& write, const state::GlobalValues& globals,
                            std::span<const state::CivilizationNames, 8> names,
                            std::span<const state::Civilization, 8> civilizations);
} // namespace openciv::formats
