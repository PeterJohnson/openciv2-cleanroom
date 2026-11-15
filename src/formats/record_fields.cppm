export module openciv.formats.record_fields;

import std;
export import openciv.formats.save_records;

export namespace openciv::formats {
// F06 §§3–4 named logical fields. The fixed save codecs and read-only
// projections share these lists; byte packing remains in the codecs.
// Unit/city/tile lists expose logical values, not host or wire layouts.
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, StoredGlobals>
void visit_fields(Visitor& visit, Record& v) {
    visit("rules", v.rules);
    visit("game_options", v.game_options);
    visit("transient_flags", v.transient_flags);
    visit("game_flags", v.game_flags);
    visit("message_options", v.message_options);
    visit("shown_event_flags", v.shown_event_flags);
    visit("retained_0e", v.retained_0e);
    visit("turn", v.turn);
    visit("year", v.year);
    visit("first_launch_turn", v.first_launch_turn);
    visit("active_unit", v.active_unit);
    visit("moving_unit", v.moving_unit);
    visit("multiplayer", v.multiplayer);
    visit("player_civ", v.player_civ);
    visit("saved_current_civ", v.saved_current_civ);
    visit("turn_civ", v.turn_civ);
    visit("view_civ", v.view_civ);
    visit("reveal_map", v.reveal_map);
    visit("difficulty", v.difficulty);
    visit("barbarian_activity", v.barbarian_activity);
    visit("civs_alive", v.civs_alive);
    visit("human_players", v.human_players);
    visit("civs_at_start", v.civs_at_start);
    visit("num_enemies", v.num_enemies);
    visit("warming_counter", v.warming_counter);
    visit("global_warmings", v.global_warmings);
    visit("warming_pollution", v.warming_pollution);
    visit("polluted_tiles", v.polluted_tiles);
    visit("peace_turns", v.peace_turns);
    visit("unit_end", v.unit_end);
    visit("city_end", v.city_end);
    visit("num_advances", v.num_advances);
    visit("retained_34", v.retained_34);
    visit("first_discoverer", v.first_discoverer);
    visit("advance_known_by", v.advance_known_by);
    visit("wonder_city", v.wonder_city);
    visit("next_history_turn", v.next_history_turn);
    visit("best_human_rank", v.best_human_rank);
    visit("worst_human_rank", v.worst_human_rank);
    visit("power_rank", v.power_rank);
    visit("civ_by_rank", v.civ_by_rank);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, StoredCivilization>
void visit_fields(Visitor& visit, Record& v) {
    visit("flags", v.flags);
    visit("gold", v.gold);
    visit("tribe", v.tribe);
    visit("research_progress", v.research_progress);
    visit("researching", v.researching);
    visit("capital_x", v.capital_x);
    visit("last_city_turn", v.last_city_turn);
    visit("num_advances", v.num_advances);
    visit("future_techs", v.future_techs);
    visit("research_cost_advances", v.research_cost_advances);
    visit("science_rate", v.science_rate);
    visit("tax_rate", v.tax_rate);
    visit("government", v.government);
    visit("diplo_random", v.diplo_random);
    visit("wonders_by_group", v.wonders_by_group);
    visit("retained_1b", v.retained_1b);
    visit("subversion_count", v.subversion_count);
    visit("reputation", v.reputation);
    visit("diplo_request_count", v.diplo_request_count);
    visit("treaties", v.treaties);
    visit("attitude", v.attitude);
    visit("reputation_credit", v.reputation_credit);
    visit("aggression_counter", v.aggression_counter);
    visit("advances", v.advances);
    visit("retained_65", v.retained_65);
    visit("military_count", v.military_count);
    visit("city_count", v.city_count);
    visit("ship_count", v.ship_count);
    visit("total_city_size", v.total_city_size);
    visit("military_strength", v.military_strength);
    visit("total_attack", v.total_attack);
    visit("population_milestone", v.population_milestone);
    visit("advance_source", v.advance_source);
    visit("units_active", v.units_active);
    visit("units_lost", v.units_lost);
    visit("units_in_production", v.units_in_production);
    visit("continent_military", v.continent_military);
    visit("continent_attack", v.continent_attack);
    visit("continent_cities", v.continent_cities);
    visit("continent_city_size", v.continent_city_size);
    visit("continent_trade_units", v.continent_trade_units);
    visit("continent_flags", v.continent_flags);
    visit("continent_policy", v.continent_policy);
    visit("best_shield_surplus", v.best_shield_surplus);
    visit("government_score", v.government_score);
    visit("last_contact_turn", v.last_contact_turn);
    visit("leader_portrait", v.leader_portrait);
    visit("throne_room", v.throne_room);
    visit("retained_3fd", v.retained_3fd);
    visit("throne_room_upgrades", v.throne_room_upgrades);
    visit("spaceship_flags", v.spaceship_flags);
    visit("retained_401", v.retained_401);
    visit("spaceship_arrival_year", v.spaceship_arrival_year);
    visit("spaceship_launch_year", v.spaceship_launch_year);
    visit("spaceship_population", v.spaceship_population);
    visit("spaceship_parts", v.spaceship_parts);
    visit("goals", v.goals);
    visit("standing_goals", v.standing_goals);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, StoredCivilizationNames>
void visit_fields(Visitor& visit, Record& v) {
    visit("city_style", v.city_style);
    visit("leader", v.leader);
    visit("tribe", v.tribe);
    visit("adjective", v.adjective);
    visit("titles", v.titles);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, StoredLeader>
void visit_fields(Visitor& visit, Record& v) {
    visit("attack", v.attack);
    visit("expand", v.expand);
    visit("civilize", v.civilize);
    visit("times_used", v.times_used);
    visit("female", v.female);
    visit("city_name_index", v.city_name_index);
    visit("color", v.color);
    visit("city_style", v.city_style);
    visit("leader_name", v.leader_name);
    visit("plural", v.plural);
    visit("adjective", v.adjective);
    visit("names", v.names);
    visit("titles", v.titles);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, SavedLayout>
void visit_fields(Visitor& visit, Record& v) {
    visit("valid", v.valid);
    visit("rectangles", v.rectangles);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, SavedMapView>
void visit_fields(Visitor& visit, Record& v) {
    visit("open", v.open);
    visit("track_moves", v.track_moves);
    visit("x", v.x);
    visit("y", v.y);
    visit("zoom", v.zoom);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, ScenarioParameters>
void visit_fields(Visitor& visit, Record& v) {
    visit("flags", v.flags);
    visit("name", v.name);
    visit("technology_paradigm", v.technology_paradigm);
    visit("year_increment", v.year_increment);
    visit("start_year", v.start_year);
    visit("maximum_turns", v.maximum_turns);
    visit("objective_civ", v.objective_civ);
    visit("victory_thresholds", v.victory_thresholds);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, EliminationHistory>
void visit_fields(Visitor& visit, Record& v) {
    visit("count", v.count);
    visit("turns", v.turns);
    visit("killers", v.killers);
    visit("portraits", v.portraits);
    visit("names", v.names);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, MultiplayerRecord>
void visit_fields(Visitor& visit, Record& v) {
    visit("center_x", v.center_x);
    visit("center_y", v.center_y);
    visit("introduced", v.introduced);
    visit("turn_time_limit", v.turn_time_limit);
    visit("encoded_passwords", v.encoded_passwords);
    visit("open_game", v.open_game);
    visit("kill_on_retirement", v.kill_on_retirement);
    visit("show_human_starts", v.show_human_starts);
    visit("chat_from_start", v.chat_from_start);
    visit("simultaneous", v.simultaneous);
    visit("cheat_turn_time", v.cheat_turn_time);
    visit("retained_142", v.retained_142);
    visit("emails", v.emails);
    visit("player_setup", v.player_setup);
    visit("resume_civ", v.resume_civ);
    visit("retained_465", v.retained_465);
    visit("resume_event", v.resume_event);
    visit("autoplay_active", v.autoplay_active);
    visit("autoplay_turns", v.autoplay_turns);
    visit("double_production", v.double_production);
    visit("double_movement", v.double_movement);
    visit("claimed_humans", v.claimed_humans);
    visit("retained_472", v.retained_472);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, EventRecord>
void visit_fields(Visitor& visit, Record& v) {
    visit("triggers", v.triggers);
    visit("actions", v.actions);
    visit("unit_name", v.unit_name);
    visit("unit_type", v.unit_type);
    visit("city_name", v.city_name);
    visit("who_name", v.who_name);
    visit("who_civ", v.who_civ);
    visit("talker_type", v.talker_type);
    visit("whom_name", v.whom_name);
    visit("whom_civ", v.whom_civ);
    visit("listener_type", v.listener_type);
    visit("turn", v.turn);
    visit("denominator", v.denominator);
    visit("technology", v.technology);
    visit("text", v.text);
    visit("move_owner_name", v.move_owner_name);
    visit("move_owner_civ", v.move_owner_civ);
    visit("move_unit_name", v.move_unit_name);
    visit("move_unit_type", v.move_unit_type);
    visit("move_count", v.move_count);
    visit("move_rectangle", v.move_rectangle);
    visit("move_x", v.move_x);
    visit("move_y", v.move_y);
    visit("aggression_whom_name", v.aggression_whom_name);
    visit("aggression_whom_civ", v.aggression_whom_civ);
    visit("aggression_who_name", v.aggression_who_name);
    visit("aggression_who_civ", v.aggression_who_civ);
    visit("create_owner_name", v.create_owner_name);
    visit("create_owner_civ", v.create_owner_civ);
    visit("create_unit_name", v.create_unit_name);
    visit("create_unit_type", v.create_unit_type);
    visit("create_locations", v.create_locations);
    visit("create_location_count", v.create_location_count);
    visit("create_veteran", v.create_veteran);
    visit("create_homecity", v.create_homecity);
    visit("auxiliary_civ_name", v.auxiliary_civ_name);
    visit("auxiliary_scalar", v.auxiliary_scalar);
    visit("auxiliary_unit_name", v.auxiliary_unit_name);
    visit("retained", v.retained);
    visit("money_receiver_name", v.money_receiver_name);
    visit("money_receiver_civ", v.money_receiver_civ);
    visit("money_amount", v.money_amount);
    visit("negotiation_flag", v.negotiation_flag);
    visit("wave_file", v.wave_file);
    visit("cd_track", v.cd_track);
    visit("terrain_type", v.terrain_type);
    visit("terrain_rectangle", v.terrain_rectangle);
    visit("destroy_civ", v.destroy_civ);
    visit("give_tech", v.give_tech);
    visit("give_tech_civ", v.give_tech_civ);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, state::CivilizationGoal>
void visit_fields(Visitor& visit, Record& v) {
    visit("x", v.x);
    visit("y", v.y);
    visit("role", v.role);
    visit("priority", v.priority);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, SavedRectangle>
void visit_fields(Visitor& visit, Record& v) {
    visit("left", v.left);
    visit("top", v.top);
    visit("right", v.right);
    visit("bottom", v.bottom);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, StoredUnit>
void visit_fields(Visitor& visit, Record& v) {
    visit("x", v.x);
    visit("y", v.y);
    visit("flags", v.flags);
    visit("type", v.type);
    visit("owner", v.owner);
    visit("moves_used", v.moves_used);
    visit("visibility", v.visibility);
    visit("damage", v.damage);
    visit("last_direction", v.last_direction);
    visit("ai_task", v.ai_task);
    visit("counter", v.counter);
    visit("auxiliary_timer", v.auxiliary_timer);
    visit("orders", v.orders);
    visit("home", v.home);
    visit("retained_byte", v.retained_byte);
    visit("destination_x", v.destination_x);
    visit("destination_y", v.destination_y);
    visit("previous", v.previous);
    visit("next", v.next);
    visit("token", v.token);
    visit("retained_word", v.retained_word);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, StoredCity>
void visit_fields(Visitor& visit, Record& v) {
    visit("x", v.x);
    visit("y", v.y);
    visit("flags", v.flags);
    visit("owner", v.owner);
    visit("size", v.size);
    visit("founder", v.founder);
    visit("turns_captured", v.turns_captured);
    visit("known_to", v.known_to);
    visit("remembered_size", v.remembered_size);
    visit("retained_byte", v.retained_byte);
    visit("specialist_kinds", v.specialist_kinds);
    visit("food_box", v.food_box);
    visit("shield_box", v.shield_box);
    visit("base_trade", v.base_trade);
    visit("name", v.name);
    visit("worked_tiles", v.worked_tiles);
    visit("improvements", v.improvements);
    visit("production", v.production);
    visit("route_count", v.route_count);
    visit("supplies", v.supplies);
    visit("demands", v.demands);
    visit("route_goods", v.route_goods);
    visit("route_partners", v.route_partners);
    visit("science", v.science);
    visit("tax", v.tax);
    visit("trade", v.trade);
    visit("total_food", v.total_food);
    visit("total_shields", v.total_shields);
    visit("happy", v.happy);
    visit("unhappy", v.unhappy);
    visit("token", v.token);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, world::Tile>
void visit_fields(Visitor& visit, Record& v) {
    visit("terrain", v.terrain);
    visit("terrain_flags", v.terrain_flags);
    visit("features", v.features);
    visit("claimant", v.claimant);
    visit("radius_other", v.radius_other);
    visit("region", v.region);
    visit("revelation", v.revelation);
    visit("fertility", v.fertility);
    visit("owner", v.owner);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, world::RegionSummary>
void visit_fields(Visitor& visit, Record& v) {
    visit("land", v.land);
    visit("fertile", v.fertile);
    visit("water", v.water);
    visit("retained", v.retained);
    visit("neighboring_water", v.neighboring_water);
}
template <class Visitor, class Record>
    requires std::same_as<std::remove_cv_t<Record>, MapHeader>
void visit_fields(Visitor& visit, Record& v) {
    visit("width", v.width);
    visit("height", v.height);
    visit("area", v.area);
    visit("topology", v.topology);
    visit("seed", v.seed);
    visit("coarse_width", v.coarse_width);
    visit("coarse_height", v.coarse_height);
}
} // namespace openciv::formats
