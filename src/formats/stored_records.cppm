export module openciv.formats.stored_records;

import std;
export import openciv.formats.fixed_text;
export import openciv.state.records;
export import openciv.state.civilization;
export import openciv.state.layout;

export extern "C++" {
    namespace openciv::formats {
    // Classic stored representations. Decode/re-encode preserves all admitted bits.
    struct StoredUnit {
        state::UnitToken token{0};
        std::int16_t x = 0, y = 0;
        std::uint16_t flags = 0;
        state::UnitType type{0};
        state::CivilizationSlot owner{0};
        std::uint8_t moves_used = 0, visibility = 0, damage = 0, last_direction = 0;
        std::uint8_t ai_task = 0, counter = 0, auxiliary_timer = 0;
        state::UnitOrder orders = state::UnitOrder::none;
        std::optional<state::CitySlot> home;
        std::int16_t destination_x = 0, destination_y = 0;
        state::SlotLink<state::UnitSlot> previous, next;
        std::uint8_t retained_byte = 0;
        std::uint16_t retained_word = 0;
        bool operator==(const StoredUnit&) const = default;
    };
    struct StoredCity {
        state::CityToken token{0};
        std::int16_t x = 0, y = 0;
        std::uint32_t flags = 0;
        state::CivilizationSlot owner{0}, founder{0};
        std::int8_t size = 0;
        std::uint8_t turns_captured = 0, known_to = 0;
        std::array<std::uint8_t, 8> remembered_size{};
        std::uint32_t specialist_kinds = 0;
        std::int16_t food_box = 0, shield_box = 0, base_trade = 0;
        FixedText<16> name;
        std::uint32_t worked_tiles = 0; // Includes specialist count and retained bits.
        std::array<std::uint8_t, 5> improvements{};
        std::int8_t production = 0;
        std::uint8_t route_count = 0;
        std::array<std::int8_t, 3> supplies{}, demands{}, route_goods{};
        std::array<state::SlotLink<state::CitySlot>, 3> route_partners{};
        std::int16_t science = 0, tax = 0, trade = 0;
        std::uint8_t total_food = 0, total_shields = 0, happy = 0, unhappy = 0;
        std::uint8_t retained_byte = 0;
        bool operator==(const StoredCity&) const = default;
    };
    struct StoredGlobals {
        std::uint16_t rules = 0;
        std::uint32_t game_options = 0;
        std::uint16_t transient_flags = 0, game_flags = 0, message_options = 0,
                      shown_event_flags = 0;
        std::uint16_t retained_0e = 0;
        std::int16_t turn = 0, year = 0, first_launch_turn = 0;
        state::SlotLink<state::UnitSlot> active_unit, moving_unit;
        std::uint8_t multiplayer = 0;
        std::int8_t player_civ = 0, saved_current_civ = 0, turn_civ = 0, view_civ = 0;
        std::uint8_t reveal_map = 0, difficulty = 0, barbarian_activity = 0;
        std::uint8_t civs_alive = 0, human_players = 0, civs_at_start = 0, num_enemies = 0;
        std::int8_t warming_counter = 0, global_warmings = 0;
        std::int16_t warming_pollution = 0, polluted_tiles = 0, peace_turns = 0;
        std::int16_t unit_end = 0, city_end = 0, num_advances = 0, retained_34 = 0;
        std::array<std::uint8_t, 100> first_discoverer{}, advance_known_by{};
        // -1 not built, -2 destroyed, otherwise a city slot (F02 contextual absence).
        std::array<std::int16_t, 28> wonder_city{};
        std::int16_t next_history_turn = 0;
        std::uint8_t best_human_rank = 0, worst_human_rank = 0;
        std::array<std::uint8_t, 8> power_rank{}, civ_by_rank{};
        bool operator==(const StoredGlobals&) const = default;
    };
    struct StoredCivilization {
        std::uint16_t flags = 0;
        std::int32_t gold = 0;
        std::int16_t tribe = 0, research_progress = 0, researching = 0;
        std::int16_t capital_x = 0, last_city_turn = 0;
        std::uint8_t num_advances = 0, future_techs = 0, research_cost_advances = 0;
        std::uint8_t science_rate = 0, tax_rate = 0, government = 0, diplo_random = 0;
        std::array<std::uint8_t, 4> wonders_by_group{};
        std::uint8_t retained_1b = 0;
        std::int16_t subversion_count = 0;
        std::uint8_t reputation = 0;
        std::int8_t diplo_request_count = 0;
        std::array<std::uint32_t, 8> treaties{};
        std::array<std::uint8_t, 8> attitude{};
        std::array<std::int8_t, 8> reputation_credit{};
        std::array<std::uint8_t, 8> aggression_counter{};
        std::array<std::uint8_t, 13> advances{};
        std::uint8_t retained_65 = 0;
        std::int16_t military_count = 0, city_count = 0, ship_count = 0, total_city_size = 0;
        std::uint16_t military_strength = 0;
        std::int16_t total_attack = 0, population_milestone = 0;
        std::array<std::uint8_t, 100> advance_source{};
        std::array<std::uint8_t, 62> units_active{}, units_lost{}, units_in_production{};
        std::array<std::uint16_t, 64> continent_military{}, continent_attack{};
        std::array<std::uint8_t, 64> continent_cities{}, continent_city_size{},
            continent_trade_units{};
        std::array<std::uint8_t, 64> continent_flags{}, continent_policy{};
        std::int16_t best_shield_surplus = 0;
        std::array<std::int16_t, 7> government_score{};
        std::array<std::int16_t, 8> last_contact_turn{};
        std::uint8_t leader_portrait = 0;
        std::array<std::uint8_t, 10> throne_room{};
        std::uint8_t retained_3fd = 0;
        std::int16_t throne_room_upgrades = 0;
        std::uint8_t spaceship_flags = 0, retained_401 = 0;
        std::int16_t spaceship_arrival_year = 0, spaceship_launch_year = 0,
                     spaceship_population = 0;
        std::array<std::int16_t, 6> spaceship_parts{};
        std::array<state::CivilizationGoal, 48> goals;
        std::array<state::CivilizationGoal, 16> standing_goals;
        bool operator==(const StoredCivilization&) const = default;
    };
    struct StoredCivilizationNames {
        std::int16_t city_style = 0;
        FixedText<24> leader, tribe, adjective;
        std::array<FixedText<24>, 7> titles;
        bool operator==(const StoredCivilizationNames&) const = default;
    };
    struct ScenarioParameters {
        std::uint16_t flags = 0;
        FixedText<80> name;
        std::int16_t technology_paradigm = 0, year_increment = 0, start_year = 0;
        std::int16_t maximum_turns = 0, objective_civ = 0;
        std::array<std::int16_t, 4> victory_thresholds{};
        bool operator==(const ScenarioParameters&) const = default;
    };
    struct EliminationHistory {
        std::int16_t count = 0;
        std::array<std::int16_t, 12> turns{};
        std::array<std::uint8_t, 12> killers{}, portraits{};
        std::array<FixedText<24>, 12> names;
        bool operator==(const EliminationHistory&) const = default;
    };
    struct MultiplayerRecord {
        std::array<std::int16_t, 8> center_x{}, center_y{}, introduced{};
        std::int32_t turn_time_limit = 0;
        std::array<std::uint8_t, 256> encoded_passwords{};
        std::int16_t open_game = 0, kill_on_retirement = 0, show_human_starts = 0;
        std::int16_t chat_from_start = 0, simultaneous = 0;
        std::int32_t cheat_turn_time = 0;
        std::array<std::uint8_t, 290> retained_142{};
        std::array<std::array<std::uint8_t, 32>, 8> emails{}, player_setup{};
        std::uint8_t resume_civ = 0, retained_465 = 0;
        std::int16_t resume_event = 0, autoplay_active = 0, autoplay_turns = 0;
        std::int16_t double_production = 0, double_movement = 0, claimed_humans = 0;
        std::array<std::uint8_t, 34> retained_472{};
        bool operator==(const MultiplayerRecord&) const = default;
    };
    struct SavedMapView {
        std::int16_t open = 0, track_moves = 0, x = 0, y = 0, zoom = 0;
        bool operator==(const SavedMapView&) const = default;
    };
    struct StoredOrdinaryPreferences {
        std::uint16_t rules = 0;
        std::int16_t default_level = 0, difficulty = 0, tribe = 0, gender = 0;
        std::int16_t science = 0, tax = 0;
        std::uint32_t game_options = 0;
        std::uint16_t disabled_messages = 0;
        std::int16_t opponents = 0, map_size = 0, barbarians = 0;
        std::array<std::int16_t, 5> world_parameters{};
        FixedText<32> text{};
        std::int16_t rules_choice = 0, accelerated_start = 0;
        bool operator==(const StoredOrdinaryPreferences&) const = default;
    };
    struct StoredMultiplayerPreferences {
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
        state::Rectangle parley{}, chat{};
        std::int16_t clear_chat_new_game = 0, clear_chat_boot = 0;
        std::int16_t double_production = 0, double_movement = 0;
        std::array<std::array<std::uint8_t, 24>, 3> legacy_settings{};
        std::array<std::array<std::uint8_t, 24>, 7> settings{};
        std::int16_t legacy_setting_5 = 0;
        bool operator==(const StoredMultiplayerPreferences&) const = default;
    };
    struct StoredEventValues {
        std::uint32_t triggers = 0, actions = 0;
        std::int32_t unit_type = 0;
        std::int32_t who_civ = 0, talker_type = 0;
        std::int32_t whom_civ = 0, listener_type = 0, turn = 0, denominator = 0, technology = 0;
        std::int32_t move_owner_civ = 0;
        std::int32_t move_unit_type = 0, move_count = 0;
        std::array<std::int32_t, 8> move_rectangle{};
        std::int32_t move_x = 0, move_y = 0;
        std::int32_t aggression_whom_civ = 0;
        std::int32_t aggression_who_civ = 0;
        std::int32_t create_owner_civ = 0;
        std::int32_t create_unit_type = 0;
        std::array<std::int32_t, 20> create_locations{};
        std::int32_t create_location_count = 0, create_veteran = 0;
        std::int32_t auxiliary_scalar = 0;
        std::int32_t money_receiver_civ = 0, money_amount = 0, negotiation_flag = 0;
        std::int32_t cd_track = 0, terrain_type = 0;
        std::array<std::int32_t, 8> terrain_rectangle{};
        std::int32_t destroy_civ = 0, give_tech = 0, give_tech_civ = 0;
        bool operator==(const StoredEventValues&) const = default;
    };
    } // namespace openciv::formats
}
