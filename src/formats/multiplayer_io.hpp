#pragma once

#include "formats/classic_io.hpp"
#include "formats/save_records.hpp"

namespace openciv::formats {
// F06 section 6 stages 11-12. Parent has checked the core version and read the
// earlier blocks. Sets local_human first; old/single-player input resets only
// the specified defaults, otherwise clears and reads the entire saved block.
// False retains the transferred prefix and earlier effects; no mode adaptation.
bool read_multiplayer_items(const ReadItem& read, std::uint16_t version,
                            const state::GlobalValues& globals, state::MultiplayerState& state,
                            state::MultiplayerPreferences& preferences,
                            base::GameplayRandom& random, std::string_view reset_call);
// F06 section 5 stage 8. The parent decides whether a multiplayer item is
// required. Temporary saved-resume substitution restores ONLY on success.
// The network turn selector is distinct from the world's stored turn selector.
bool write_multiplayer_item(const WriteItem& write, state::MultiplayerState& state,
                            std::int32_t network_turn_civ);
} // namespace openciv::formats
