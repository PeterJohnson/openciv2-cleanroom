import std;
import openciv.state.preferences;

namespace openciv::state {
void default_ordinary_preferences(OrdinaryPreferences& p, const PreferenceDefaults& defaults) {
    p.rules = 0x003f;
    p.default_level = defaults.screen_width > 999 ? 2 : 0;
    p.difficulty = Difficulty::chieftain;
    p.gender = Gender::male;
    p.tribe = 0;
    p.science = 6;
    p.tax = 4;
    p.game_options = 0x003f3258;
    if (!defaults.animations_available)
        p.game_options &= ~0x00200000U;
    p.disabled_messages = 0;
    p.opponents = 0;
    p.map_size = WorldSize::normal;
    p.barbarians = 2;
    p.world_parameters.fill(0);
    p.text.clear();
    p.rules_choice = RuleSelection::standard;
    p.accelerated_start = 0;
}
void default_multiplayer_preferences(MultiplayerPreferences& p) {
    p.main_menu = p.type = p.hotseat_menu = p.lan_menu = p.email_menu = 0;
    p.protocol = NetworkProtocol::tcp;
    p.open_game = p.kill_on_retirement = p.show_human_starts = p.chat_from_start = false;
    p.simultaneous = p.double_production = p.double_movement = false;
    p.game_name.clear();
    p.player_name.clear();
    p.email_address.clear();
    p.ip_address.clear();
    p.timer = p.hotseat_players = 0;
    p.custom_timer = "60";
    p.parley = p.chat = {};
    p.clear_chat_new_game = p.clear_chat_boot = false;
}
} // namespace openciv::state
