// This consumer deliberately links only oc2_contracts, not the game/session/codecs.
import std;
import openciv.state.identities;
import openciv.state.unit_types;
import openciv.session.player_setup_types;
import openciv.session.world_setup_types;
import openciv.session.configuration_types;

using namespace openciv;
static_assert(!std::is_copy_constructible_v<session::PlayerSetupInteraction>);
static_assert(!std::is_copy_constructible_v<session::WorldSizeInteraction>);
static_assert(!std::is_copy_constructible_v<session::MusicControl>);
static_assert(!std::is_copy_constructible_v<session::OptionVoteChannel>);
class SizeChoice final : public session::WorldSizeInteraction {
public:
    session::WorldSizeChoice choose_preset(state::WorldSize) const override {
        return session::CustomWorldSize{};
    }
    session::CustomSizeResponse choose_custom(std::string_view width,
                                              std::string_view height) const override {
        return {session::CustomSizeStatus::accepted, std::string(width), std::string(height)};
    }
};
int main() {
    SizeChoice choice;
    return std::holds_alternative<session::CustomWorldSize>(
               choice.choose_preset(state::WorldSize::normal)) &&
                   choice.choose_custom("50", "80").width == "50"
               ? 0
               : 1;
}
