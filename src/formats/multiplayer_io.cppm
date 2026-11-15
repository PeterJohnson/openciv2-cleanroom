export module openciv.formats.multiplayer_io;
import openciv.formats.state_conversion;

import std;
export import openciv.formats.classic_io;
export import openciv.formats.save_records;

export namespace openciv::formats {
/// Write the multiplayer record with the supplied network turn and opaque retained bytes.
bool write_multiplayer_item(const WriteItem& write, const state::MultiplayerState& state,
                            std::int32_t network_turn_civ,
                            const RecordCompatibility& compatibility = {});
} // namespace openciv::formats

module :private;
namespace openciv::formats {
bool write_multiplayer_item(const WriteItem& write, const state::MultiplayerState& state,
                            std::int32_t network_turn_civ,
                            const RecordCompatibility& compatibility) {
    auto saved = to_stored(state.saved);
    if (compatibility.multiplayer)
        preserve_opaque(saved, *compatibility.multiplayer);
    saved.resume_civ = static_cast<std::uint8_t>(network_turn_civ);
    return write(encode_multiplayer(saved));
}
} // namespace openciv::formats
