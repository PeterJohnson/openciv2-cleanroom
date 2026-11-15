#pragma once
#include "formats/classic_io.hpp"
#include "formats/save_records.hpp"
#include "state/rule_state.hpp"

namespace openciv::formats {
StoredLeader stored_leader(const state::Leader& leader);
struct SavedCursor {
    std::int16_t x = 0, y = 0;
};
bool write_leader_items(const WriteItem& write, const state::GlobalValues& globals,
                        std::span<const state::Civilization> civilizations,
                        std::span<const state::Leader> leaders, const SavedCursor& cursor);
} // namespace openciv::formats
