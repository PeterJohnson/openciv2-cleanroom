export module openciv.formats.leader_io;

import std;
export import openciv.formats.classic_io;
export import openciv.formats.save_records;
export import openciv.state.rule_state;

export extern "C++" {
    namespace openciv::formats {
    StoredLeader stored_leader(const state::Leader& leader);
    struct SavedCursor {
        std::int16_t x = 0, y = 0;
    };
    bool write_leader_items(const WriteItem& write, const state::GlobalValues& globals,
                            std::span<const state::Civilization> civilizations,
                            std::span<const state::Leader> leaders, const SavedCursor& cursor);
    } // namespace openciv::formats
}
