#pragma once

#include "state/layout.hpp"
#include "state/records.hpp"

namespace openciv::state {
// F02 process lifetime; F03 section 8 preferences. Construction is not the
// default operation. F06 codecs alone define the two CIV2.DAT item layouts.
struct OrdinaryPreferences {
    std::uint16_t rules = 0;
    std::int16_t default_level = 0, difficulty = 0, tribe = 0, gender = 0;
    std::int16_t science = 0, tax = 0;
    std::uint32_t game_options = 0;
    std::uint16_t disabled_messages = 0;
    std::int16_t opponents = 0, map_size = 0, barbarians = 0;
    std::array<std::int16_t, 5> world_parameters{};
    FixedText<32> text{};
    std::int16_t rules_choice = 0, accelerated_start = 0;
    bool operator==(const OrdinaryPreferences&) const = default;
};
struct MultiplayerPreferences {
    // These remembered choices are distinct from saved/live session values.
    std::int16_t kill_on_retirement = 0, show_human_starts = 0, chat_from_start = 0;
    std::int16_t simultaneous = 0;
    std::int16_t main_menu = 0, type = 0, hotseat_menu = 0, lan_menu = 0;
    std::int16_t legacy_setting_1 = 0, email_menu = 0;
    std::uint16_t protocol = 0;
    std::int16_t open_game = 0;
    FixedText<32> game_name{}, player_name{}, email_address{}, ip_address{};
    std::int16_t timer = 0;
    FixedText<6> custom_timer{};
    std::int16_t hotseat_players = 0;
    Rectangle parley{}, chat{};
    std::int16_t clear_chat_new_game = 0, clear_chat_boot = 0;
    std::int16_t double_production = 0, double_movement = 0;
    std::array<std::array<std::uint8_t, 24>, 3> legacy_settings{};
    std::array<std::array<std::uint8_t, 24>, 7> settings{};
    std::int16_t legacy_setting_5 = 0;
    bool operator==(const MultiplayerPreferences&) const = default;
};
struct PreferenceDefaults {
    // Supplied startup screen/renderer outcomes. No original memory API or
    // renderer allocation requirement is inferred from these logical inputs.
    std::int32_t screen_width = 0;
    bool animations_available = false;
};
void default_ordinary_preferences(OrdinaryPreferences& preferences,
                                  const PreferenceDefaults& defaults) noexcept;
void default_multiplayer_preferences(MultiplayerPreferences& preferences) noexcept;
} // namespace openciv::state
