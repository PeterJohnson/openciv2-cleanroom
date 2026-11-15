export module openciv.state.preferences;

import std;
export import openciv.state.choices;
export import openciv.state.layout;
export import openciv.state.records;

export extern "C++" {
    namespace openciv::state {
    // F02 process lifetime; F03 section 8 preferences. Construction is not the
    // default operation. F06 codecs alone define the two CIV2.DAT item layouts.
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
    struct PreferenceDefaults {
        // Supplied startup screen/renderer outcomes. No original memory API or
        // renderer allocation requirement is inferred from these logical inputs.
        std::int32_t screen_width = 0;
        bool animations_available = false;
    };
    void default_ordinary_preferences(OrdinaryPreferences& preferences,
                                      const PreferenceDefaults& defaults);
    void default_multiplayer_preferences(MultiplayerPreferences& preferences);
    } // namespace openciv::state
}
