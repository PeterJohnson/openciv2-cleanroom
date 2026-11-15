export module openciv.formats.multiplayer_io;

import std;
export import openciv.formats.classic_io;
export import openciv.formats.save_records;

export extern "C++" {
    namespace openciv::formats {
    bool write_multiplayer_item(const WriteItem& write, const state::MultiplayerState& state,
                                std::int32_t network_turn_civ,
                                const RecordCompatibility& compatibility = {});
    } // namespace openciv::formats
}
