import std;
import openciv.formats.state_conversion;
import openciv.formats.record_compatibility;

namespace openciv::formats {
state::Unit to_live(const StoredUnit& value) {
    state::Unit result;
    result.token = value.token;
    result.x = value.x;
    result.y = value.y;
    result.flags = value.flags;
    result.type = value.type;
    result.owner = value.owner;
    result.moves_used = value.moves_used;
    result.visibility = value.visibility;
    result.damage = value.damage;
    result.last_direction = value.last_direction;
    result.ai_task = value.ai_task;
    result.counter = value.counter;
    result.auxiliary_timer = value.auxiliary_timer;
    result.orders = value.orders;
    result.home = value.home;
    result.destination_x = value.destination_x;
    result.destination_y = value.destination_y;
    result.previous = value.previous;
    result.next = value.next;
    return result;
}
StoredUnit to_stored(const state::Unit& value) {
    StoredUnit result;
    result.token = value.token;
    result.x = value.x;
    result.y = value.y;
    result.flags = value.flags;
    result.type = value.type;
    result.owner = value.owner;
    result.moves_used = value.moves_used;
    result.visibility = value.visibility;
    result.damage = value.damage;
    result.last_direction = value.last_direction;
    result.ai_task = value.ai_task;
    result.counter = value.counter;
    result.auxiliary_timer = value.auxiliary_timer;
    result.orders = value.orders;
    result.home = value.home;
    result.destination_x = value.destination_x;
    result.destination_y = value.destination_y;
    result.previous = value.previous;
    result.next = value.next;
    return result;
}
state::City to_live(const StoredCity& value) {
    state::City result;
    result.token = value.token;
    result.x = value.x;
    result.y = value.y;
    result.flags = value.flags;
    result.owner = value.owner;
    result.founder = value.founder;
    result.size = value.size;
    result.turns_captured = value.turns_captured;
    result.known_to = value.known_to;
    result.remembered_size = value.remembered_size;
    result.specialist_kinds = value.specialist_kinds;
    result.food_box = value.food_box;
    result.shield_box = value.shield_box;
    result.base_trade = value.base_trade;
    result.name = to_live(value.name);
    result.worked_tiles = value.worked_tiles;
    result.improvements = value.improvements;
    result.production = value.production;
    result.route_count = value.route_count;
    result.supplies = value.supplies;
    result.demands = value.demands;
    result.route_goods = value.route_goods;
    result.route_partners = value.route_partners;
    result.science = value.science;
    result.tax = value.tax;
    result.trade = value.trade;
    result.total_food = value.total_food;
    result.total_shields = value.total_shields;
    result.happy = value.happy;
    result.unhappy = value.unhappy;
    return result;
}
StoredCity to_stored(const state::City& value) {
    StoredCity result;
    result.token = value.token;
    result.x = value.x;
    result.y = value.y;
    result.flags = value.flags;
    result.owner = value.owner;
    result.founder = value.founder;
    result.size = value.size;
    result.turns_captured = value.turns_captured;
    result.known_to = value.known_to;
    result.remembered_size = value.remembered_size;
    result.specialist_kinds = value.specialist_kinds;
    result.food_box = value.food_box;
    result.shield_box = value.shield_box;
    result.base_trade = value.base_trade;
    result.name = stored_text<16>(value.name);
    result.worked_tiles = value.worked_tiles;
    result.improvements = value.improvements;
    result.production = value.production;
    result.route_count = value.route_count;
    result.supplies = value.supplies;
    result.demands = value.demands;
    result.route_goods = value.route_goods;
    result.route_partners = value.route_partners;
    result.science = value.science;
    result.tax = value.tax;
    result.trade = value.trade;
    result.total_food = value.total_food;
    result.total_shields = value.total_shields;
    result.happy = value.happy;
    result.unhappy = value.unhappy;
    return result;
}
state::GlobalValues to_live(const StoredGlobals& value) {
    state::GlobalValues result;
    result.rules = value.rules;
    result.game_options = value.game_options;
    result.transient_flags = value.transient_flags;
    result.game_flags = value.game_flags;
    result.message_options = value.message_options;
    result.shown_event_flags = value.shown_event_flags;
    result.turn = value.turn;
    result.year = value.year;
    result.first_launch_turn = value.first_launch_turn;
    result.active_unit = value.active_unit;
    result.moving_unit = value.moving_unit;
    result.multiplayer = static_cast<state::GameMode>(value.multiplayer);
    result.player_civ = value.player_civ;
    result.saved_current_civ = value.saved_current_civ;
    result.turn_civ = value.turn_civ;
    result.view_civ = value.view_civ;
    result.reveal_map = value.reveal_map;
    result.difficulty = static_cast<state::Difficulty>(value.difficulty);
    result.barbarian_activity = value.barbarian_activity;
    result.civs_alive = value.civs_alive;
    result.human_players = value.human_players;
    result.civs_at_start = value.civs_at_start;
    result.num_enemies = value.num_enemies;
    result.warming_counter = value.warming_counter;
    result.global_warmings = value.global_warmings;
    result.warming_pollution = value.warming_pollution;
    result.polluted_tiles = value.polluted_tiles;
    result.peace_turns = value.peace_turns;
    result.unit_end = value.unit_end;
    result.city_end = value.city_end;
    result.num_advances = value.num_advances;
    result.first_discoverer = value.first_discoverer;
    result.advance_known_by = value.advance_known_by;
    result.wonder_city = value.wonder_city;
    result.next_history_turn = value.next_history_turn;
    result.best_human_rank = value.best_human_rank;
    result.worst_human_rank = value.worst_human_rank;
    result.power_rank = value.power_rank;
    result.civ_by_rank = value.civ_by_rank;
    return result;
}
StoredGlobals to_stored(const state::GlobalValues& value) {
    StoredGlobals result;
    result.rules = value.rules;
    result.game_options = value.game_options;
    result.transient_flags = value.transient_flags;
    result.game_flags = value.game_flags;
    result.message_options = value.message_options;
    result.shown_event_flags = value.shown_event_flags;
    result.turn = value.turn;
    result.year = value.year;
    result.first_launch_turn = value.first_launch_turn;
    result.active_unit = value.active_unit;
    result.moving_unit = value.moving_unit;
    result.multiplayer = static_cast<decltype(result.multiplayer)>(value.multiplayer);
    result.player_civ = value.player_civ;
    result.saved_current_civ = value.saved_current_civ;
    result.turn_civ = value.turn_civ;
    result.view_civ = value.view_civ;
    result.reveal_map = value.reveal_map;
    result.difficulty = static_cast<decltype(result.difficulty)>(value.difficulty);
    result.barbarian_activity = value.barbarian_activity;
    result.civs_alive = value.civs_alive;
    result.human_players = value.human_players;
    result.civs_at_start = value.civs_at_start;
    result.num_enemies = value.num_enemies;
    result.warming_counter = value.warming_counter;
    result.global_warmings = value.global_warmings;
    result.warming_pollution = value.warming_pollution;
    result.polluted_tiles = value.polluted_tiles;
    result.peace_turns = value.peace_turns;
    result.unit_end = value.unit_end;
    result.city_end = value.city_end;
    result.num_advances = value.num_advances;
    result.first_discoverer = value.first_discoverer;
    result.advance_known_by = value.advance_known_by;
    result.wonder_city = value.wonder_city;
    result.next_history_turn = value.next_history_turn;
    result.best_human_rank = value.best_human_rank;
    result.worst_human_rank = value.worst_human_rank;
    result.power_rank = value.power_rank;
    result.civ_by_rank = value.civ_by_rank;
    return result;
}
state::Civilization to_live(const StoredCivilization& value) {
    state::Civilization result;
    result.flags = value.flags;
    result.gold = value.gold;
    result.tribe = value.tribe;
    result.research_progress = value.research_progress;
    result.researching = value.researching;
    result.capital_x = value.capital_x;
    result.last_city_turn = value.last_city_turn;
    result.num_advances = value.num_advances;
    result.future_techs = value.future_techs;
    result.research_cost_advances = value.research_cost_advances;
    result.science_rate = value.science_rate;
    result.tax_rate = value.tax_rate;
    result.government = static_cast<state::Government>(value.government);
    result.diplo_random = value.diplo_random;
    result.wonders_by_group = value.wonders_by_group;
    result.subversion_count = value.subversion_count;
    result.reputation = value.reputation;
    result.diplo_request_count = value.diplo_request_count;
    result.treaties = value.treaties;
    result.attitude = value.attitude;
    result.reputation_credit = value.reputation_credit;
    result.aggression_counter = value.aggression_counter;
    result.advances = value.advances;
    result.military_count = value.military_count;
    result.city_count = value.city_count;
    result.ship_count = value.ship_count;
    result.total_city_size = value.total_city_size;
    result.military_strength = value.military_strength;
    result.total_attack = value.total_attack;
    result.population_milestone = value.population_milestone;
    result.advance_source = value.advance_source;
    result.units_active = value.units_active;
    result.units_lost = value.units_lost;
    result.units_in_production = value.units_in_production;
    result.continent_military = value.continent_military;
    result.continent_attack = value.continent_attack;
    result.continent_cities = value.continent_cities;
    result.continent_city_size = value.continent_city_size;
    result.continent_trade_units = value.continent_trade_units;
    result.continent_flags = value.continent_flags;
    result.continent_policy = value.continent_policy;
    result.best_shield_surplus = value.best_shield_surplus;
    result.government_score = value.government_score;
    result.last_contact_turn = value.last_contact_turn;
    result.leader_portrait = value.leader_portrait;
    result.throne_room = value.throne_room;
    result.throne_room_upgrades = value.throne_room_upgrades;
    result.spaceship_flags = value.spaceship_flags;
    result.spaceship_arrival_year = value.spaceship_arrival_year;
    result.spaceship_launch_year = value.spaceship_launch_year;
    result.spaceship_population = value.spaceship_population;
    result.spaceship_parts = value.spaceship_parts;
    result.goals = value.goals;
    result.standing_goals = value.standing_goals;
    return result;
}
StoredCivilization to_stored(const state::Civilization& value) {
    StoredCivilization result;
    result.flags = value.flags;
    result.gold = value.gold;
    result.tribe = value.tribe;
    result.research_progress = value.research_progress;
    result.researching = value.researching;
    result.capital_x = value.capital_x;
    result.last_city_turn = value.last_city_turn;
    result.num_advances = value.num_advances;
    result.future_techs = value.future_techs;
    result.research_cost_advances = value.research_cost_advances;
    result.science_rate = value.science_rate;
    result.tax_rate = value.tax_rate;
    result.government = static_cast<std::uint8_t>(value.government);
    result.diplo_random = value.diplo_random;
    result.wonders_by_group = value.wonders_by_group;
    result.subversion_count = value.subversion_count;
    result.reputation = value.reputation;
    result.diplo_request_count = value.diplo_request_count;
    result.treaties = value.treaties;
    result.attitude = value.attitude;
    result.reputation_credit = value.reputation_credit;
    result.aggression_counter = value.aggression_counter;
    result.advances = value.advances;
    result.military_count = value.military_count;
    result.city_count = value.city_count;
    result.ship_count = value.ship_count;
    result.total_city_size = value.total_city_size;
    result.military_strength = value.military_strength;
    result.total_attack = value.total_attack;
    result.population_milestone = value.population_milestone;
    result.advance_source = value.advance_source;
    result.units_active = value.units_active;
    result.units_lost = value.units_lost;
    result.units_in_production = value.units_in_production;
    result.continent_military = value.continent_military;
    result.continent_attack = value.continent_attack;
    result.continent_cities = value.continent_cities;
    result.continent_city_size = value.continent_city_size;
    result.continent_trade_units = value.continent_trade_units;
    result.continent_flags = value.continent_flags;
    result.continent_policy = value.continent_policy;
    result.best_shield_surplus = value.best_shield_surplus;
    result.government_score = value.government_score;
    result.last_contact_turn = value.last_contact_turn;
    result.leader_portrait = value.leader_portrait;
    result.throne_room = value.throne_room;
    result.throne_room_upgrades = value.throne_room_upgrades;
    result.spaceship_flags = value.spaceship_flags;
    result.spaceship_arrival_year = value.spaceship_arrival_year;
    result.spaceship_launch_year = value.spaceship_launch_year;
    result.spaceship_population = value.spaceship_population;
    result.spaceship_parts = value.spaceship_parts;
    result.goals = value.goals;
    result.standing_goals = value.standing_goals;
    return result;
}
state::CivilizationNames to_live(const StoredCivilizationNames& value) {
    state::CivilizationNames result;
    result.city_style = value.city_style;
    result.leader = to_live(value.leader);
    result.tribe = to_live(value.tribe);
    result.adjective = to_live(value.adjective);
    for (std::size_t i = 0; i < result.titles.size(); ++i)
        result.titles[i] = to_live(value.titles[i]);
    return result;
}
StoredCivilizationNames to_stored(const state::CivilizationNames& value) {
    StoredCivilizationNames result;
    result.city_style = value.city_style;
    result.leader = stored_text<24>(value.leader);
    result.tribe = stored_text<24>(value.tribe);
    result.adjective = stored_text<24>(value.adjective);
    for (std::size_t i = 0; i < result.titles.size(); ++i)
        result.titles[i] = stored_text<24>(value.titles[i]);
    return result;
}
state::ScenarioParameters to_live(const ScenarioParameters& value) {
    state::ScenarioParameters result;
    result.flags = value.flags;
    result.name = to_live(value.name);
    result.technology_paradigm = value.technology_paradigm;
    result.year_increment = value.year_increment;
    result.start_year = value.start_year;
    result.maximum_turns = value.maximum_turns;
    result.objective_civ = value.objective_civ;
    result.victory_thresholds = value.victory_thresholds;
    return result;
}
ScenarioParameters to_stored(const state::ScenarioParameters& value) {
    ScenarioParameters result;
    result.flags = value.flags;
    result.name = stored_text<80>(value.name);
    result.technology_paradigm = value.technology_paradigm;
    result.year_increment = value.year_increment;
    result.start_year = value.start_year;
    result.maximum_turns = value.maximum_turns;
    result.objective_civ = value.objective_civ;
    result.victory_thresholds = value.victory_thresholds;
    return result;
}
state::EliminationHistory to_live(const EliminationHistory& value) {
    state::EliminationHistory result;
    result.count = value.count;
    result.turns = value.turns;
    result.killers = value.killers;
    result.portraits = value.portraits;
    for (std::size_t i = 0; i < result.names.size(); ++i)
        result.names[i] = to_live(value.names[i]);
    return result;
}
EliminationHistory to_stored(const state::EliminationHistory& value) {
    EliminationHistory result;
    result.count = value.count;
    result.turns = value.turns;
    result.killers = value.killers;
    result.portraits = value.portraits;
    for (std::size_t i = 0; i < result.names.size(); ++i)
        result.names[i] = stored_text<24>(value.names[i]);
    return result;
}
state::MultiplayerValues to_live(const MultiplayerRecord& value) {
    state::MultiplayerValues result;
    result.center_x = value.center_x;
    result.center_y = value.center_y;
    for (std::size_t i = 0; i < result.introduced.size(); ++i)
        result.introduced[i] = value.introduced[i] != 0;
    result.turn_time_limit = value.turn_time_limit;
    result.encoded_passwords = value.encoded_passwords;
    result.open_game = value.open_game != 0;
    result.kill_on_retirement = value.kill_on_retirement != 0;
    result.show_human_starts = value.show_human_starts != 0;
    result.chat_from_start = value.chat_from_start != 0;
    result.simultaneous = value.simultaneous != 0;
    result.cheat_turn_time = value.cheat_turn_time;
    for (std::size_t i = 0; i < result.emails.size(); ++i)
        result.emails[i] = to_live(FixedText<32>{value.emails[i]});
    result.resume_civ = value.resume_civ;
    result.resume_event = value.resume_event;
    result.autoplay_active = value.autoplay_active != 0;
    result.autoplay_turns = value.autoplay_turns;
    result.double_production = value.double_production != 0;
    result.double_movement = value.double_movement != 0;
    result.claimed_humans = value.claimed_humans;
    return result;
}
MultiplayerRecord to_stored(const state::MultiplayerValues& value) {
    MultiplayerRecord result;
    result.center_x = value.center_x;
    result.center_y = value.center_y;
    for (std::size_t i = 0; i < result.introduced.size(); ++i)
        result.introduced[i] = value.introduced[i] ? 1 : 0;
    result.turn_time_limit = value.turn_time_limit;
    result.encoded_passwords = value.encoded_passwords;
    result.open_game = value.open_game ? 1 : 0;
    result.kill_on_retirement = value.kill_on_retirement ? 1 : 0;
    result.show_human_starts = value.show_human_starts ? 1 : 0;
    result.chat_from_start = value.chat_from_start ? 1 : 0;
    result.simultaneous = value.simultaneous ? 1 : 0;
    result.cheat_turn_time = value.cheat_turn_time;
    for (std::size_t i = 0; i < result.emails.size(); ++i)
        result.emails[i] = stored_text<32>(value.emails[i]).bytes;
    result.resume_civ = value.resume_civ;
    result.resume_event = value.resume_event;
    result.autoplay_active = value.autoplay_active ? 1 : 0;
    result.autoplay_turns = value.autoplay_turns;
    result.double_production = value.double_production ? 1 : 0;
    result.double_movement = value.double_movement ? 1 : 0;
    result.claimed_humans = value.claimed_humans;
    return result;
}
state::MapView to_live(const SavedMapView& value) {
    state::MapView result;
    result.open = value.open != 0;
    result.track_moves = value.track_moves != 0;
    result.x = value.x;
    result.y = value.y;
    result.zoom = value.zoom;
    return result;
}
SavedMapView to_stored(const state::MapView& value) {
    SavedMapView result;
    result.open = value.open ? 1 : 0;
    result.track_moves = value.track_moves ? 1 : 0;
    result.x = value.x;
    result.y = value.y;
    result.zoom = value.zoom;
    return result;
}
state::OrdinaryPreferences to_live(const StoredOrdinaryPreferences& value) {
    state::OrdinaryPreferences result;
    result.rules = value.rules;
    result.default_level = value.default_level;
    result.difficulty = static_cast<state::Difficulty>(value.difficulty);
    result.tribe = value.tribe;
    result.gender = static_cast<state::Gender>(value.gender);
    result.science = value.science;
    result.tax = value.tax;
    result.game_options = value.game_options;
    result.disabled_messages = value.disabled_messages;
    result.opponents = value.opponents;
    result.map_size = static_cast<state::WorldSize>(value.map_size);
    result.barbarians = value.barbarians;
    result.world_parameters = value.world_parameters;
    result.text = to_live(value.text);
    result.rules_choice = static_cast<state::RuleSelection>(value.rules_choice);
    result.accelerated_start = value.accelerated_start;
    return result;
}
StoredOrdinaryPreferences to_stored(const state::OrdinaryPreferences& value) {
    StoredOrdinaryPreferences result;
    result.rules = value.rules;
    result.default_level = value.default_level;
    result.difficulty = static_cast<decltype(result.difficulty)>(value.difficulty);
    result.tribe = value.tribe;
    result.gender = static_cast<decltype(result.gender)>(value.gender);
    result.science = value.science;
    result.tax = value.tax;
    result.game_options = value.game_options;
    result.disabled_messages = value.disabled_messages;
    result.opponents = value.opponents;
    result.map_size = static_cast<decltype(result.map_size)>(value.map_size);
    result.barbarians = value.barbarians;
    result.world_parameters = value.world_parameters;
    result.text = stored_text<32>(value.text);
    result.rules_choice = static_cast<decltype(result.rules_choice)>(value.rules_choice);
    result.accelerated_start = value.accelerated_start;
    return result;
}
state::MultiplayerPreferences to_live(const StoredMultiplayerPreferences& value) {
    state::MultiplayerPreferences result;
    result.kill_on_retirement = value.kill_on_retirement != 0;
    result.show_human_starts = value.show_human_starts != 0;
    result.chat_from_start = value.chat_from_start != 0;
    result.simultaneous = value.simultaneous != 0;
    result.main_menu = value.main_menu;
    result.type = value.type;
    result.hotseat_menu = value.hotseat_menu;
    result.lan_menu = value.lan_menu;
    result.email_menu = value.email_menu;
    result.protocol = static_cast<state::NetworkProtocol>(value.protocol);
    result.open_game = value.open_game != 0;
    result.game_name = to_live(value.game_name);
    result.player_name = to_live(value.player_name);
    result.email_address = to_live(value.email_address);
    result.ip_address = to_live(value.ip_address);
    result.timer = value.timer;
    result.custom_timer = to_live(value.custom_timer);
    result.hotseat_players = value.hotseat_players;
    result.parley = value.parley;
    result.chat = value.chat;
    result.clear_chat_new_game = value.clear_chat_new_game != 0;
    result.clear_chat_boot = value.clear_chat_boot != 0;
    result.double_production = value.double_production != 0;
    result.double_movement = value.double_movement != 0;
    return result;
}
StoredMultiplayerPreferences to_stored(const state::MultiplayerPreferences& value) {
    StoredMultiplayerPreferences result;
    result.kill_on_retirement = value.kill_on_retirement ? 1 : 0;
    result.show_human_starts = value.show_human_starts ? 1 : 0;
    result.chat_from_start = value.chat_from_start ? 1 : 0;
    result.simultaneous = value.simultaneous ? 1 : 0;
    result.main_menu = value.main_menu;
    result.type = value.type;
    result.hotseat_menu = value.hotseat_menu;
    result.lan_menu = value.lan_menu;
    result.email_menu = value.email_menu;
    result.protocol = static_cast<decltype(result.protocol)>(value.protocol);
    result.open_game = value.open_game ? 1 : 0;
    result.game_name = stored_text<32>(value.game_name);
    result.player_name = stored_text<32>(value.player_name);
    result.email_address = stored_text<32>(value.email_address);
    result.ip_address = stored_text<32>(value.ip_address);
    result.timer = value.timer;
    result.custom_timer = stored_text<6>(value.custom_timer);
    result.hotseat_players = value.hotseat_players;
    result.parley = value.parley;
    result.chat = value.chat;
    result.clear_chat_new_game = value.clear_chat_new_game ? 1 : 0;
    result.clear_chat_boot = value.clear_chat_boot ? 1 : 0;
    result.double_production = value.double_production ? 1 : 0;
    result.double_movement = value.double_movement ? 1 : 0;
    return result;
}
state::EventValues to_live(const StoredEventValues& value) {
    state::EventValues result;
    result.triggers = value.triggers;
    result.actions = value.actions;
    result.unit_type = value.unit_type;
    result.who_civ = value.who_civ;
    result.talker_type = value.talker_type;
    result.whom_civ = value.whom_civ;
    result.listener_type = value.listener_type;
    result.turn = value.turn;
    result.denominator = value.denominator;
    result.technology = value.technology;
    result.move_owner_civ = value.move_owner_civ;
    result.move_unit_type = value.move_unit_type;
    result.move_count = value.move_count;
    result.move_rectangle = value.move_rectangle;
    result.move_x = value.move_x;
    result.move_y = value.move_y;
    result.aggression_whom_civ = value.aggression_whom_civ;
    result.aggression_who_civ = value.aggression_who_civ;
    result.create_owner_civ = value.create_owner_civ;
    result.create_unit_type = value.create_unit_type;
    result.create_locations = value.create_locations;
    result.create_location_count = value.create_location_count;
    result.create_veteran = value.create_veteran != 0;
    result.auxiliary_scalar = value.auxiliary_scalar;
    result.money_receiver_civ = value.money_receiver_civ;
    result.money_amount = value.money_amount;
    result.negotiation_flag = value.negotiation_flag != 0;
    result.cd_track = value.cd_track;
    result.terrain_type = value.terrain_type;
    result.terrain_rectangle = value.terrain_rectangle;
    result.destroy_civ = value.destroy_civ;
    result.give_tech = value.give_tech;
    result.give_tech_civ = value.give_tech_civ;
    return result;
}
StoredEventValues to_stored(const state::EventValues& value) {
    StoredEventValues result;
    result.triggers = value.triggers;
    result.actions = value.actions;
    result.unit_type = value.unit_type;
    result.who_civ = value.who_civ;
    result.talker_type = value.talker_type;
    result.whom_civ = value.whom_civ;
    result.listener_type = value.listener_type;
    result.turn = value.turn;
    result.denominator = value.denominator;
    result.technology = value.technology;
    result.move_owner_civ = value.move_owner_civ;
    result.move_unit_type = value.move_unit_type;
    result.move_count = value.move_count;
    result.move_rectangle = value.move_rectangle;
    result.move_x = value.move_x;
    result.move_y = value.move_y;
    result.aggression_whom_civ = value.aggression_whom_civ;
    result.aggression_who_civ = value.aggression_who_civ;
    result.create_owner_civ = value.create_owner_civ;
    result.create_unit_type = value.create_unit_type;
    result.create_locations = value.create_locations;
    result.create_location_count = value.create_location_count;
    result.create_veteran = value.create_veteran ? 1 : 0;
    result.auxiliary_scalar = value.auxiliary_scalar;
    result.money_receiver_civ = value.money_receiver_civ;
    result.money_amount = value.money_amount;
    result.negotiation_flag = value.negotiation_flag ? 1 : 0;
    result.cd_track = value.cd_track;
    result.terrain_type = value.terrain_type;
    result.terrain_rectangle = value.terrain_rectangle;
    result.destroy_civ = value.destroy_civ;
    result.give_tech = value.give_tech;
    result.give_tech_civ = value.give_tech_civ;
    return result;
}
void preserve_opaque(StoredUnit& output, const StoredUnit& imported) {
    output.retained_byte = imported.retained_byte;
    output.retained_word = imported.retained_word;
}
void preserve_opaque(StoredCity& output, const StoredCity& imported) {
    output.retained_byte = imported.retained_byte;
}
void preserve_opaque(StoredGlobals& output, const StoredGlobals& imported) {
    output.retained_0e = imported.retained_0e;
    output.retained_34 = imported.retained_34;
}
void preserve_opaque(StoredCivilization& output, const StoredCivilization& imported) {
    output.retained_1b = imported.retained_1b;
    output.retained_65 = imported.retained_65;
    output.retained_3fd = imported.retained_3fd;
    output.retained_401 = imported.retained_401;
}
void preserve_opaque(MultiplayerRecord& output, const MultiplayerRecord& imported) {
    output.retained_142 = imported.retained_142;
    output.retained_465 = imported.retained_465;
    output.retained_472 = imported.retained_472;
    output.player_setup = imported.player_setup;
}
} // namespace openciv::formats
