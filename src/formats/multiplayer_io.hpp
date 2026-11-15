#pragma once
#include "formats/classic_io.hpp"
#include "formats/save_records.hpp"

namespace openciv::formats {
bool write_multiplayer_item(const WriteItem& write, const state::MultiplayerState& state,
                            std::int32_t network_turn_civ);
} // namespace openciv::formats
