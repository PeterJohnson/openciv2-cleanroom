export module openciv.state.preferences;

import std;
export import openciv.state.choices;
export import openciv.state.layout;
export import openciv.state.records;

export namespace openciv::state {
/// F02 process lifetime; F03 section 8 preferences. Construction is not the
/// default operation. F06 codecs alone define the two CIV2.DAT item layouts.
struct OrdinaryPreferences {
    std::uint16_t rules = 0;
    std::int16_t default_level = 0, tribe = 0;
    Difficulty difficulty = Difficulty::chieftain;
    Gender gender = Gender::male;
    std::int16_t science = 0, tax = 0;
    std::uint32_t game_options = 0;
    std::uint16_t disabled_messages = 0;
    std::int16_t opponents = 0, barbarians = 0;
    WorldSize map_size = WorldSize::small;
    std::array<std::int16_t, 5> world_parameters{};
    std::string text{};
    std::int16_t accelerated_start = 0;
    RuleSelection rules_choice = RuleSelection::standard;
    bool operator==(const OrdinaryPreferences&) const = default;
};
/// Live multiplayer preferences, independent of serialized record layout.
struct MultiplayerPreferences {
    // These remembered choices are distinct from saved/live session values.
    bool kill_on_retirement = false, show_human_starts = false, chat_from_start = false;
    bool simultaneous = false;
    std::int16_t main_menu = 0, type = 0, hotseat_menu = 0, lan_menu = 0;
    std::int16_t email_menu = 0;
    NetworkProtocol protocol = NetworkProtocol::tcp;
    bool open_game = false;
    std::string game_name{}, player_name{}, email_address{}, ip_address{};
    std::int16_t timer = 0;
    std::string custom_timer{};
    std::int16_t hotseat_players = 0;
    Rectangle parley{}, chat{};
    bool clear_chat_new_game = false, clear_chat_boot = false;
    bool double_production = false, double_movement = false;
    bool operator==(const MultiplayerPreferences&) const = default;
};
/// Owner-supplied environment values used when establishing ordinary defaults.
struct PreferenceDefaults {
    // Supplied startup screen/renderer outcomes. No original memory API or
    // renderer allocation requirement is inferred from these logical inputs.
    std::int32_t screen_width = 0;
    bool animations_available = false;
};
/// Apply F03 ordinary preference defaults using explicit environment inputs.
void default_ordinary_preferences(OrdinaryPreferences& preferences,
                                  const PreferenceDefaults& defaults);
/// Apply F03 multiplayer preference defaults.
void default_multiplayer_preferences(MultiplayerPreferences& preferences);
} // namespace openciv::state

module :private;
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
