#include <catch2/catch_test_macros.hpp>

import std;
import openciv.formats.state_conversion;
import openciv.session.runtime;
import openciv.formats.preference_io;
import openciv.formats.save_records;

// X01 fresh-game isolation, F02 section 2 and the admitted session contract.
// These are implementation foundation tests, not original-game captures.

using namespace openciv;
using namespace openciv::session;
namespace {
void poison(state::GameState& world, state::ProcessContext& process) {
    const auto unit = formats::decode_unit(formats::Bytes(formats::unit_record_size, 0xa5));
    const auto city = formats::decode_city(formats::Bytes(formats::city_record_size, 0x5a));
    std::fill(world.entities.units.storage().begin(), world.entities.units.storage().end(),
              formats::to_live(unit));
    std::fill(world.entities.cities.storage().begin(), world.entities.cities.storage().end(),
              formats::to_live(city));
    world.entities.units.set_end(2);
    world.entities.cities.set_end(1);
    world.unit_stacks = {9, true, true, true, true, true, 11, 12, 13, 14};
    world.unit_deletion = {true, -2};
    world.unit_totals.fill(123);
    world.turn_loop_active = true;
    world.movement_mode = static_cast<state::MovementMode>(99);
    world.civilization_color_flags.fill(0xffffffffU);
    world.globals = formats::to_live(
        formats::decode_globals(formats::Bytes(formats::global_record_size, 0xa5)));
    world.world_setup = {40, 50, {1, -1, 2, -2, 3}};
    for (auto& civ : world.civilizations)
        civ = formats::to_live(
            formats::decode_civilization(formats::Bytes(formats::civilization_record_size, 0xa5)));
    for (auto& names : world.names) {
        names.city_style = 2;
        names.leader = std::string(24, static_cast<char>(7));
        names.tribe = std::string(24, static_cast<char>(8));
        names.adjective = std::string(24, static_cast<char>(9));
        for (auto& title : names.titles)
            title = std::string(24, static_cast<char>(10));
    }
    world.pending_leader_overrides.fill(7);
    world.multiplayer.saved = formats::to_live(
        formats::decode_multiplayer(formats::Bytes(formats::multiplayer_record_size, 0xa5)));
    world.multiplayer.passwords.pending.fill(true);
    world.multiplayer.passwords.present.fill(true);
    world.multiplayer.local_human = world.multiplayer.resume_civ = 7;
    world.multiplayer.hotseat_participants = 254;
    world.multiplayer.network_turn_civ = 6;
    world.multiplayer.network_host = true;
    world.multiplayer.player_count = world.multiplayer.handoff = 5;
    world.multiplayer.vote = {4, 3, 2};
    world.events.loaded = true;
    world.events.collection.append()->set_string(0, state::EventString{"previous event"});
    for (auto& row : world.power_history)
        row.fill(91);
    world.eliminations = formats::to_live(formats::decode_elimination_history(
        formats::Bytes(formats::elimination_record_size, 0x5a)));
    world.scenario = formats::to_live(formats::decode_scenario_parameters(
        formats::Bytes(formats::scenario_parameters_size, 0xa5)));
    world.layout.valid = 1;
    world.layout.rectangles.fill({1, 2, 3, 4});
    world.views.fill({true, true, 3, 4, 5});
    world.mood = {state::CitySlot{2}, 40, -20};
    world.map.allocate(world::Geometry{8, 6});
    for (auto& tile : world.map.tiles()) {
        tile.features = 255;
        tile.region = 11;
        tile.revelation = 254;
    }
    for (unsigned civ = 1; civ < 8; ++civ)
        std::fill(world.map.remembered_layer(civ).begin(), world.map.remembered_layer(civ).end(),
                  77);
    for (unsigned grid = 0; grid < 4; ++grid)
        std::fill(world.map.coarse_grid(grid).begin(), world.map.coarse_grid(grid).end(), 98);
    world.map.pattern_seed = 112;
    world.map.stored_topology = 1;
    world.map.regions.fill({1, 2, 3, 4, {5, 6}});
    world.map.remembered_barbarian = {1, 2, 3};
    process.fallback.region = 62;
    process.fallback.features = 255;
    process.text_parameters.numbers.fill(-123);
    process.text_parameters.strings.fill("old substitution");
    process.text_parameters.line = "old line";
    process.rules->text.initialize();
    const auto name = process.rules->text.store("old rule name");
    process.rules->names.units.fill(name);
    process.rules->labels.entries = {name};
    process.rules->labels.popups.fill(name);
    process.rules->labels.declared_count = 1;
    process.rules->labels.status_language = 3;
    process.rules->definitions.units[0].attack = 123;
    process.rules->definitions.units[0].identity.id = "old-unit";
    for (auto& leader : process.rules->leaders) {
        leader.attack = -91;
        leader.times_used = leader.city_name_index = 165;
        leader.leader_name = leader.plural = leader.adjective = {70000, true};
        leader.names.fill({80000, true});
        leader.titles.fill({state::NameReference{90000, true}, state::NameReference{100000, true}});
    }
    process.rules->double_movement_applied = true;
}

class Hold final : public Continuation {
public:
    OperationStep resume(OperationContext& context, OperationInput input) override {
        if (input.kind == OperationInputKind::cancel)
            return {CommandStatus::cancelled, {}, "cancelled", {}};
        context.world.civilizations[1].gold -= 5;
        context.effect("paid", {{"amount", std::int32_t{5}}});
        InteractionSpec prompt;
        prompt.prompt = "Continue?";
        prompt.choices = {1};
        return {CommandStatus::awaiting_interaction, {}, "waiting", prompt};
    }
};
CommandDefinition hold_definition() {
    CommandDefinition result;
    result.descriptor.name = "test.hold";
    result.descriptor.help = "Foundation lifecycle probe";
    result.admit = [](const auto&, const auto&, const auto&) { return Admission{}; };
    result.execute = [](const auto&) { return std::make_unique<Hold>(); };
    return result;
}
} // namespace

TEST_CASE(
    "Fresh-game replacement clears world backing and mutable content and preserves the allowlist",
    "[session][isolation]") {
    state::ProcessContext process, cold_process;
    SessionRuntime runtime{process, RuntimeId{18}};
    runtime.begin_client_session({13, 17});
    auto& gameplay = runtime.gameplay_random();
    auto& presentation = runtime.presentation_random();
    gameplay.primary_modulo("prior.game", 5);
    presentation.primary_modulo("prior.content", 3);
    const auto random_before = gameplay.checkpoint();
    const auto presentation_before = presentation.checkpoint();
    const auto client =
        runtime.bind(runtime.add_participant(state::CivilizationSlot{1}), ControllerKind::scripted);
    const auto identity = client.identity();
    poison(runtime.world(), process);
    process.ordinary_preferences = formats::to_live(formats::decode_ordinary_preferences(
        formats::Bytes(formats::ordinary_preferences_size, 0xa5)));
    process.multiplayer_preferences = formats::to_live(formats::decode_multiplayer_preferences(
        formats::Bytes(formats::multiplayer_preferences_size, 0x5a)));
    process.tokens = {4294967295U, 0};
    const auto ordinary = process.ordinary_preferences;
    const auto multiplayer = process.multiplayer_preferences;
    auto* rules_address = process.rules.get();
    runtime.prepare_new_game();
    CHECK_FALSE(runtime.active());
    CHECK_THROWS_AS(client.identity(), AccessError);
    CHECK_THROWS_AS(client.own_units(), AccessError);
    CHECK(process.tokens == state::ProcessTokens{4294967295U, 0});
    CHECK(process.ordinary_preferences == ordinary);
    CHECK(process.multiplayer_preferences == multiplayer);
    CHECK(gameplay.checkpoint() == random_before);
    CHECK(presentation.checkpoint() == presentation_before);
    CHECK(process.rules.get() == rules_address);
    CHECK(process.rules->definitions == cold_process.rules->definitions);
    CHECK(process.rules->names == state::RuleNameBindings{});
    CHECK(process.rules->leaders == cold_process.rules->leaders);
    CHECK_FALSE(process.rules->double_movement_applied);
    CHECK(process.rules->text.size() == 1);
    CHECK(process.rules->text.text(0) == "<nil>");
    CHECK(process.rules->labels.entries.empty());
    CHECK(process.rules->labels.popups == state::Labels{}.popups);
    CHECK(process.rules->labels.declared_count == 0);
    CHECK(process.rules->labels.status_language == 0);
    CHECK(process.text_parameters.numbers == state::TextParameters{}.numbers);
    CHECK(process.text_parameters.strings == state::TextParameters{}.strings);
    CHECK(process.text_parameters.line.empty());
    CHECK(process.fallback == world::Tile{});
    const auto& world = runtime.world();
    state::GameState cold{cold_process};
    for (auto& civ : cold.civilizations) {
        civ.goals.fill({0, 0, -1, 0});
        civ.standing_goals.fill({0, 0, -1, 0});
    }
    CHECK(world.entities.units.end() == 0);
    CHECK(world.entities.cities.end() == 0);
    CHECK(std::equal(world.entities.units.storage().begin(), world.entities.units.storage().end(),
                     cold.entities.units.storage().begin()));
    CHECK(std::equal(world.entities.cities.storage().begin(), world.entities.cities.storage().end(),
                     cold.entities.cities.storage().begin()));
    CHECK(world.unit_stacks.suppression == 0);
    CHECK_FALSE(world.unit_stacks.animation_active);
    CHECK_FALSE(world.unit_stacks.detach_active);
    CHECK_FALSE(world.unit_stacks.attach_active);
    CHECK_FALSE(world.unit_stacks.relocate_active);
    CHECK_FALSE(world.unit_stacks.bottom_active);
    CHECK(world.unit_stacks.detach_reply == 0);
    CHECK(world.unit_stacks.attach_reply == 0);
    CHECK(world.unit_stacks.relocate_reply == 0);
    CHECK(world.unit_stacks.bottom_reply == 0);
    CHECK_FALSE(world.unit_deletion.active);
    CHECK(world.unit_deletion.reply == 0);
    CHECK(world.unit_totals == cold.unit_totals);
    CHECK_FALSE(world.turn_loop_active);
    CHECK(world.movement_mode == state::MovementMode::view);
    CHECK(world.civilization_color_flags == cold.civilization_color_flags);
    CHECK(world.globals == cold.globals);
    CHECK(world.world_setup == cold.world_setup);
    CHECK(world.civilizations == cold.civilizations);
    CHECK(world.names == cold.names);
    CHECK(world.pending_leader_overrides == cold.pending_leader_overrides);
    CHECK(world.multiplayer == cold.multiplayer);
    CHECK_FALSE(world.events.loaded);
    CHECK(world.events.collection.size() == 0);
    CHECK(world.power_history == cold.power_history);
    CHECK(world.eliminations == cold.eliminations);
    CHECK(world.scenario == cold.scenario);
    CHECK(world.layout == cold.layout);
    CHECK(world.views == cold.views);
    CHECK(world.mood == cold.mood);
    CHECK_FALSE(world.map.allocated());
    CHECK_THROWS_AS(world.map.geometry(), std::logic_error);
    CHECK_THROWS_AS(world.map.tiles(), std::logic_error);
    CHECK(world.map.pattern_seed == 0);
    CHECK(world.map.stored_topology == 0);
    CHECK(world.map.regions == cold.map.regions);
    CHECK(world.map.remembered_barbarian.empty());
    runtime.begin_client_session({13, 17});
    const auto new_client =
        runtime.bind(runtime.add_participant(state::CivilizationSlot{1}), ControllerKind::scripted);
    CHECK(new_client.identity().session != identity.session);
    CHECK(new_client.identity().participant != identity.participant);
    CHECK(new_client.identity().controller != identity.controller);
    CHECK_THROWS_AS(client.identity(), AccessError);
    CHECK(new_client.own_units().records.empty());
}

TEST_CASE("Fresh-game rejection keeps pending interactions effects and required replies intact",
          "[session][isolation]") {
    state::ProcessContext process;
    SessionRuntime runtime{process, RuntimeId{18}};
    runtime.register_command(hold_definition());
    runtime.begin_client_session({13, 17});
    const auto seat = runtime.add_participant(state::CivilizationSlot{1}, ConnectionId{9});
    const auto client = runtime.bind(seat, ControllerKind::scripted);
    const CommandCorrelation origin{RuntimeId{29}, SessionId{7}, CommandId{41}};
    const auto request = runtime.receive_request(ConnectionId{9}, seat, origin, {"test.hold", {}});
    REQUIRE(request.status == CommandStatus::pending);
    REQUIRE(runtime.advance_one());
    const auto pending = client.result(request.correlation.command);
    REQUIRE(pending);
    REQUIRE(pending->status == CommandStatus::awaiting_interaction);
    REQUIRE(pending->committed.size() == 1);
    auto* world_before = &runtime.world();
    auto* rules_before = process.rules.get();
    CHECK_THROWS_AS(runtime.prepare_new_game(), std::logic_error);
    CHECK(&runtime.world() == world_before);
    CHECK(process.rules.get() == rules_before);
    CHECK(client.result(request.correlation.command) == pending);
    CHECK(runtime.world().civilizations[1].gold == -5);
    REQUIRE(runtime.cancel_command(request.correlation.command).accepted);
    REQUIRE(runtime.advance_one());
    CHECK_FALSE(runtime.has_pending_commands());
    CHECK_THROWS_AS(runtime.prepare_new_game(), std::logic_error);
    CHECK(runtime.world().civilizations[1].gold == -5);
    const auto replies = runtime.take_replies();
    REQUIRE(replies.size() == 1);
    CHECK(replies[0].correlation == origin);
    CHECK(replies[0].destination == ConnectionId{9});
    CHECK(replies[0].status == CommandStatus::cancelled);
    runtime.prepare_new_game();
    CHECK(runtime.world().civilizations[1].gold == 0);
    CHECK_THROWS_AS(client.updates(), AccessError);
}
