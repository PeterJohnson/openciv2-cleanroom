export module openciv.formats.gameplay;

import std;
export import openciv.formats.content_source;
export import openciv.formats.content_json;
export import openciv.state.gameplay;

export namespace openciv::formats::content {
inline constexpr std::string_view gameplay_target = "classic-mge-5.4.0f";
// Trusted repository ledger docs/content/gameplay-defaults.json; a user pack
// cannot replace this baseline or certify its own changed operands as classic.
inline constexpr std::string_view classic_gameplay_sha256 =
    "d3276f6cfe815b9b8412e2930a4d3acd49b831bb11277b21779b53448356a940";
/// Validated gameplay data with its manifest and component receipts.
struct GameplayBundle {
    state::GameplayValues values;
    std::string id, revision, manifest_sha256, semantic_sha256;
    bool custom_mode = false, classic_values = false;
    std::array<FileReceipt, 6> files;
    // Advance references still require the selected rules; this is not admission
    // to a session, checkpoint, classic file or original-peer connection.
};
/// Load a gameplay manifest and its explicitly resolved components, checking compatibility.
std::shared_ptr<const GameplayBundle> load_gameplay(std::span<const Byte> manifest,
                                                    const Resolver& resolve,
                                                    bool require_classic = false);
} // namespace openciv::formats::content
