#include "state/preferences.hpp"

namespace openciv::state {
void default_ordinary_preferences(OrdinaryPreferences& p,
                                  const PreferenceDefaults& defaults) noexcept {
    p.rules = 0x003f;
    p.default_level = defaults.screen_width > 999 ? 2 : 0;
    p.difficulty = p.tribe = p.gender = 0;
    p.science = 6;
    p.tax = 4;
    p.game_options = 0x003f3258;
    if (!defaults.animations_available)
        p.game_options &= ~0x00200000U;
    p.disabled_messages = 0;
    p.opponents = 0;
    p.map_size = 1;
    p.barbarians = 2;
    p.world_parameters.fill(0);
    p.text.bytes[0] = 0;
    p.rules_choice = p.accelerated_start = 0;
}
void default_multiplayer_preferences(MultiplayerPreferences& p) noexcept {
    p.main_menu = p.type = p.hotseat_menu = p.lan_menu = p.email_menu = 0;
    p.protocol = 0;
    p.open_game = p.kill_on_retirement = p.show_human_starts = p.chat_from_start = 0;
    p.simultaneous = p.double_production = p.double_movement = 0;
    p.game_name.bytes[0] = p.player_name.bytes[0] = p.email_address.bytes[0] =
        p.ip_address.bytes[0] = 0;
    p.timer = p.hotseat_players = 0;
    p.custom_timer.bytes[0] = '6';
    p.custom_timer.bytes[1] = '0';
    p.custom_timer.bytes[2] = 0;
    p.parley = p.chat = {};
    p.clear_chat_new_game = p.clear_chat_boot = 0;
    p.legacy_setting_1 = p.legacy_setting_5 = 0;
    for (auto& setting : p.legacy_settings)
        setting[0] = 0;
    for (auto& setting : p.settings)
        setting.fill(0);
}
} // namespace openciv::state
