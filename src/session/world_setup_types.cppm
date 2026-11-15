export module openciv.session.world_setup_types;

import std;
export import openciv.state.choices;
export namespace openciv::session {
enum class WorldSource { generated, premade_cylindrical, premade_flat };
struct CustomWorldSize {};
using WorldSizeChoice = std::variant<std::int32_t, CustomWorldSize>;
enum class CustomSizeStatus { accepted, cancelled, unavailable };
struct CustomSizeResponse {
    CustomSizeStatus status = CustomSizeStatus::cancelled;
    std::string width, height;
};
class WorldSizeInteraction {
public:
    WorldSizeInteraction() = default;
    virtual ~WorldSizeInteraction() = default;
    WorldSizeInteraction(const WorldSizeInteraction&) = delete;
    WorldSizeInteraction& operator=(const WorldSizeInteraction&) = delete;
    // Ordinary 0=Small, 2=Large, other nonnegative=Normal; negative=cancel.
    virtual WorldSizeChoice choose_preset(state::WorldSize remembered_preset) const = 0;
    virtual CustomSizeResponse choose_custom(std::string_view width_default,
                                             std::string_view height_default) const = 0;
};

struct RandomWorldParameter {};
using WorldParameterChoice = std::variant<std::int32_t, RandomWorldParameter>;

} // namespace openciv::session
