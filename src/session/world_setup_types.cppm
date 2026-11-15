export module openciv.session.world_setup_types;

import std;
export import openciv.state.choices;
export namespace openciv::session {
/// Source and topology policy of a new world.
enum class WorldSource { generated, premade_cylindrical, premade_flat };
/// Explicit selection of the custom world-size dialog.
struct CustomWorldSize {};
using WorldSizeChoice = std::variant<std::int32_t, CustomWorldSize>;
/// Distinguish accepted, cancelled, and unavailable custom-size input.
enum class CustomSizeStatus { accepted, cancelled, unavailable };
/// Custom-size outcome and the unparsed width/height text.
struct CustomSizeResponse {
    CustomSizeStatus status = CustomSizeStatus::cancelled;
    std::string width, height;
};
/// Borrowed size-selection interactions used by world setup.
class WorldSizeInteraction {
public:
    WorldSizeInteraction() = default;
    virtual ~WorldSizeInteraction() = default;
    WorldSizeInteraction(const WorldSizeInteraction&) = delete;
    WorldSizeInteraction& operator=(const WorldSizeInteraction&) = delete;
    /// Ordinary 0=Small, 2=Large, other nonnegative=Normal; negative=cancel.
    virtual WorldSizeChoice choose_preset(state::WorldSize remembered_preset) const = 0;
    /// Request custom dimensions using the supplied display defaults.
    virtual CustomSizeResponse choose_custom(std::string_view width_default,
                                             std::string_view height_default) const = 0;
};

/// Explicit request to sample a world parameter instead of selecting a row.
struct RandomWorldParameter {};
using WorldParameterChoice = std::variant<std::int32_t, RandomWorldParameter>;

} // namespace openciv::session
