#include "formats/multiplayer_io.hpp"

namespace openciv::formats {
bool write_multiplayer_item(const WriteItem& write, const state::MultiplayerState& state,
                            std::int32_t network_turn_civ) {
    auto saved = state.saved;
    saved.resume_civ = static_cast<std::uint8_t>(network_turn_civ);
    return write(encode_multiplayer(saved));
}
} // namespace openciv::formats
