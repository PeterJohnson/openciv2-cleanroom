// Architecture §§3–5 session/query contracts and F02 §§2–3 lifetimes. These
// are authored foundation tests, not original turn-loop/setup conformance.
#include "session/runtime.hpp"
#include <catch2/catch_test_macros.hpp>
#include <type_traits>

using namespace openciv;
using namespace openciv::session;
namespace {
constexpr state::CivilizationSlot first{1}, second{2};
void populate(state::GameState& world) {
    auto units = world.entities.units.storage();
    world.entities.units.set_end(5);
    for (std::size_t i = 0; i < 6; ++i) {
        units[i].token = state::UnitToken{static_cast<std::uint32_t>(100 + i)};
        units[i].owner = i % 2 ? second : first;
        units[i].x = static_cast<std::int16_t>(i * 2);
    }
    units[2].token = state::UnitToken{0}; // A hole is not an own-unit observation.
    units[5].owner = first;               // Nor is a retained occupied slot above the scan bound.
    auto cities = world.entities.cities.storage();
    world.entities.cities.set_end(4);
    for (std::size_t i = 0; i < 5; ++i) {
        cities[i].token = state::CityToken{static_cast<std::uint32_t>(200 + i)};
        cities[i].owner = i % 2 ? second : first;
        cities[i].name.bytes.fill(0xee);
        cities[i].name.bytes[0] = static_cast<std::uint8_t>('A' + i);
        cities[i].name.bytes[1] = 0;
    }
    cities[2].token = state::CityToken{0};
    world.globals.human_players = 2;
    world.civilizations[1].military_count = -77; // Queries must not rebuild censuses.
    world.mood.happy = 123;
}
void revoked(const PlayerClient& client) {
    CHECK_THROWS_AS(client.identity(), AccessError);
    CHECK_THROWS_AS(client.own_units(), AccessError);
    CHECK_THROWS_AS(client.own_cities(), AccessError);
}
} // namespace

TEST_CASE("Session clients receive stable owned projections without mutating state",
          "[session][query]") {
    static_assert(!std::is_default_constructible_v<PlayerClient>);
    static_assert(!std::is_convertible_v<ParticipantId, ControllerId>);
    static_assert(!std::is_convertible_v<ControllerId, ConnectionId>);
    state::ProcessContext process;
    SessionRuntime runtime(process, RuntimeId{7});
    const auto session = runtime.begin_client_session({11, 22});
    populate(runtime.world());
    runtime.publish();
    const auto p1 = runtime.add_participant(first);
    const auto p2 = runtime.add_participant(second, ConnectionId{42});
    const auto client = runtime.bind(p1, ControllerKind::frontend);
    const auto other = runtime.bind(p2, ControllerKind::scripted);
    auto claimed_identity = client.identity();
    claimed_identity.observer = second;
    claimed_identity.access = AccessClass::developer;
    CHECK(client.identity().observer != claimed_identity.observer);
    CHECK(client.identity().access != claimed_identity.access);
    const auto tokens = process.tokens;
    const auto globals = runtime.world().globals;
    const auto civilization = runtime.world().civilizations[1];
    const auto mood = runtime.world().mood;
    const auto gameplay = runtime.gameplay_random().checkpoint();
    const auto presentation = runtime.presentation_random().checkpoint();
    const auto raw_units = runtime.world().entities.units.scanned();
    const std::vector<state::Unit> units_before(raw_units.begin(), raw_units.end());
    const auto raw_cities = runtime.world().entities.cities.scanned();
    const std::vector<state::City> cities_before(raw_cities.begin(), raw_cities.end());
    const auto units = client.own_units();
    REQUIRE(units.records.size() == 2);
    CHECK(units.records[0].slot == state::UnitSlot{0});
    CHECK(units.records[1].slot == state::UnitSlot{4});
    CHECK(units.records[1].token == state::UnitToken{104});
    CHECK(units.observation == query::LiveObservation{RuntimeId{7}, session, 1, first});
    const auto cities = client.own_cities();
    REQUIRE(cities.records.size() == 1);
    CHECK(cities.records[0].slot == state::CitySlot{0});
    CHECK(cities.records[0].name == std::vector<std::uint8_t>{'A'}); // No retained name tail.
    const auto theirs = other.own_units();
    REQUIRE(theirs.records.size() == 2);
    CHECK(theirs.records[0].slot == state::UnitSlot{1});
    CHECK(theirs.records[1].slot == state::UnitSlot{3});
    CHECK(other.own_cities().records.size() == 2);
    CHECK(std::equal(raw_units.begin(), raw_units.end(), units_before.begin()));
    CHECK(std::equal(raw_cities.begin(), raw_cities.end(), cities_before.begin()));
    CHECK(process.tokens == tokens);
    CHECK(runtime.world().globals == globals);
    CHECK(runtime.world().civilizations[1] == civilization);
    CHECK(runtime.world().mood == mood);
    CHECK(runtime.gameplay_random().checkpoint() == gameplay);
    CHECK(runtime.presentation_random().checkpoint() == presentation);
    // Returned values remain their original observation after publication.
    runtime.world().entities.units.at(state::UnitSlot{0}).damage = 9;
    runtime.world().entities.cities.at(state::CitySlot{0}).name.bytes[0] = 'Z';
    runtime.publish();
    CHECK(units.records[0].damage == 0);
    CHECK(cities.records[0].name == std::vector<std::uint8_t>{'A'});
    CHECK(client.own_units().records[0].damage == 9);
    CHECK(client.own_units().observation.revision == 2);
    CHECK(client.own_cities().records[0].name == std::vector<std::uint8_t>{'Z'});
}

TEST_CASE("Controller changes leave human and participant identities independent", "[session]") {
    state::ProcessContext process;
    SessionRuntime runtime(process, RuntimeId{1});
    runtime.begin_client_session({0, 0});
    populate(runtime.world());
    const auto participant = runtime.add_participant(first, ConnectionId{9});
    const auto frontend = runtime.bind(participant, ControllerKind::frontend);
    const auto automation = runtime.bind(participant, ControllerKind::automation);
    CHECK(frontend.identity().participant == automation.identity().participant);
    CHECK(frontend.identity().controller != automation.identity().controller);
    CHECK(frontend.own_units() == automation.own_units());
    CHECK(runtime.participant(participant).connection == ConnectionId{9});
    const auto frontend_copy = frontend;
    runtime.revoke(frontend.identity().controller);
    revoked(frontend);
    revoked(frontend_copy);
    CHECK(automation.own_units().records.size() == 2);
    const auto policy = runtime.bind(participant, ControllerKind::engine_policy);
    CHECK(policy.identity().kind == ControllerKind::engine_policy);
    CHECK(runtime.world().globals.human_players == 2);
    CHECK(runtime.participant(participant) == Participant{participant, first, ConnectionId{9}});
    // The explicit developer grant does not make the own-entity query omniscient.
    const auto developer =
        runtime.bind(participant, ControllerKind::scripted, AccessClass::developer);
    CHECK(developer.own_units() == automation.own_units());
}

TEST_CASE("Participant reassignment and session replacement revoke all stale clients",
          "[session]") {
    state::ProcessContext process;
    SessionRuntime runtime(process, RuntimeId{1});
    const auto old_session = runtime.begin_client_session({5, 6});
    populate(runtime.world());
    const auto seat = runtime.add_participant(first);
    const auto old = runtime.bind(seat, ControllerKind::frontend);
    const auto old_automation = runtime.bind(seat, ControllerKind::automation);
    const auto unrelated = runtime.bind(runtime.add_participant(second), ControllerKind::scripted);
    runtime.reassign_participant(seat, second, ConnectionId{17});
    revoked(old);
    revoked(old_automation);
    CHECK(unrelated.identity().observer == second);
    const auto rebound = runtime.bind(seat, ControllerKind::frontend);
    CHECK(rebound.identity().observer == second);
    CHECK(rebound.own_units().records.front().slot == state::UnitSlot{1});
    runtime.remove_participant(seat);
    revoked(rebound);
    CHECK_THROWS_AS(runtime.bind(seat, ControllerKind::frontend), std::out_of_range);
    const auto fresh = runtime.begin_client_session({5, 6});
    CHECK(fresh != old_session);
    revoked(unrelated);
    CHECK_THROWS_AS(runtime.participant(seat), std::out_of_range);
    const auto current = runtime.bind(runtime.add_participant(first), ControllerKind::frontend);
    CHECK(current.identity().session == fresh);
    runtime.end_client_session();
    revoked(current);
    CHECK_FALSE(runtime.active());
    CHECK_THROWS_AS(runtime.add_participant(first), AccessError);
    CHECK_THROWS_AS(runtime.gameplay_random(), AccessError);
}

TEST_CASE("Runtime destruction invalidates clients without extending world ownership",
          "[session]") {
    state::ProcessContext process;
    std::optional<PlayerClient> client;
    {
        SessionRuntime runtime(process, RuntimeId{1});
        runtime.begin_client_session({1, 2});
        client = runtime.bind(runtime.add_participant(first), ControllerKind::frontend);
        CHECK(client->own_units().records.empty());
    }
    revoked(*client);
}

TEST_CASE("Disconnect revokes only mapped input access and retains world effects", "[session]") {
    state::ProcessContext process;
    SessionRuntime runtime(process, RuntimeId{1});
    runtime.begin_client_session({0, 0});
    populate(runtime.world());
    const auto p1 = runtime.add_participant(first, ConnectionId{9});
    const auto p2 = runtime.add_participant(second, ConnectionId{10});
    const auto p3 = runtime.add_participant(state::CivilizationSlot{3}, ConnectionId{9});
    const auto client = runtime.bind(p1, ControllerKind::frontend);
    const auto automation = runtime.bind(p1, ControllerKind::automation);
    const auto same_connection = runtime.bind(p3, ControllerKind::scripted);
    const auto other = runtime.bind(p2, ControllerKind::scripted);
    runtime.world().entities.units.at(state::UnitSlot{0}).damage = 17;
    runtime.publish();
    runtime.disconnect(ConnectionId{9});
    revoked(client);
    revoked(automation);
    revoked(same_connection);
    CHECK(other.identity().participant == p2);
    CHECK(runtime.participant(p1) == Participant{p1, first, {}});
    CHECK(runtime.participant(p3).connection == std::nullopt);
    CHECK(runtime.participant(p2).connection == ConnectionId{10});
    CHECK(runtime.world().entities.units.at(state::UnitSlot{0}).damage == 17);
    CHECK(runtime.world().globals.human_players == 2);
    runtime.disconnect(ConnectionId{9}); // An already-disconnected endpoint is a no-op.
    CHECK(other.own_units().records.size() == 2);
}

TEST_CASE("Session ownership retains process values and isolates virtual peers", "[session][F02]") {
    state::ProcessContext process, peer_process;
    SessionRuntime runtime(process, RuntimeId{1}), peer(peer_process, RuntimeId{2});
    runtime.begin_client_session({91, 92});
    peer.begin_client_session({91, 92});
    populate(runtime.world());
    process.tokens.assign_unit();
    process.tokens.assign_city();
    process.rules->leaders[4].city_name_index = 73;
    process.rules->text.initialize();
    const auto label = process.rules->text.store("Retained rule");
    process.rules->names.units[3] = label;
    process.rules->definitions.units[3].attack = 91;
    process.fallback.fertility = 99;
    runtime.world().map.allocate(world::Geometry{4, 2});
    runtime.world().map.regions[0].land = -3;
    runtime.world().map.tiles()[0].fertility = 42;
    const auto a = runtime.bind(runtime.add_participant(first), ControllerKind::frontend);
    const auto b = peer.bind(peer.add_participant(first), ControllerKind::frontend);
    CHECK(a.own_units().observation.runtime != b.own_units().observation.runtime);
    CHECK(b.own_units().records.empty());
    CHECK(peer_process.tokens == state::ProcessTokens{});
    CHECK(peer_process.rules->leaders[4].city_name_index == 0);
    CHECK(peer_process.rules->text.size() == 0);
    CHECK(peer_process.rules->definitions.units[3].attack == 0);
    CHECK(peer_process.fallback.fertility == 0);
    // A client-session transition itself must not pretend to be F05 new-game reset.
    runtime.begin_client_session({93, 94});
    CHECK(process.tokens == state::ProcessTokens{2, 2});
    CHECK(process.rules->leaders[4].city_name_index == 73);
    CHECK(process.rules->text.text(process.rules->names.units[3]) == "Retained rule");
    CHECK(process.rules->definitions.units[3].attack == 91);
    CHECK(runtime.world().entities.units.end() == 5);
    CHECK(runtime.world().map.tiles()[0].fertility == 42);
    CHECK(runtime.world().map.regions[0].land == -3);
    CHECK(&runtime.process() == &process);
    // The separately specified inner entity reset keeps tokens/backing/cursors.
    std::optional<state::UnitSlot> selected;
    state::reset_entity_scans(runtime.world().entities, selected);
    CHECK(runtime.world().entities.units.end() == 0);
    CHECK(runtime.world().entities.cities.end() == 0);
    CHECK(selected == state::UnitSlot{0});
    CHECK(runtime.world().entities.units.at(state::UnitSlot{4}).token == state::UnitToken{104});
    CHECK(process.tokens.assign_unit() == state::UnitToken{2});
    CHECK(process.rules->leaders[4].city_name_index == 73);
}

TEST_CASE("Session query and presentation consumers cannot advance gameplay contexts",
          "[session][F01]") {
    state::ProcessContext p1, p2;
    SessionRuntime with_view(p1, RuntimeId{1}), headless(p2, RuntimeId{2});
    with_view.begin_client_session({101, 202});
    headless.begin_client_session({101, 202});
    const auto client = with_view.bind(with_view.add_participant(first), ControllerKind::frontend);
    base::CosmeticRandom cosmetic(303, "frontend");
    for (unsigned i = 0; i < 100; ++i) {
        client.own_units();
        client.own_cities();
        with_view.presentation_random().primary_modulo("test.content", 5);
        cosmetic.primary_modulo("test.cosmetic", 7);
        CHECK(with_view.gameplay_random().secondary_range("test.gameplay", -10, 20) ==
              headless.gameplay_random().secondary_range("test.gameplay", -10, 20));
    }
    CHECK(with_view.gameplay_random().checkpoint() == headless.gameplay_random().checkpoint());
    CHECK(with_view.presentation_random().checkpoint() !=
          headless.presentation_random().checkpoint());
}

TEST_CASE("Session grants validate identity domains before issuing access", "[session]") {
    state::ProcessContext process;
    CHECK_THROWS_AS((SessionRuntime{process, RuntimeId{0}}), std::invalid_argument);
    SessionRuntime runtime(process, RuntimeId{1});
    CHECK_THROWS_AS(runtime.bind(ParticipantId{1}, ControllerKind::frontend), AccessError);
    runtime.begin_client_session({0, 0});
    CHECK_THROWS_AS(runtime.add_participant(state::CivilizationSlot{8}), std::invalid_argument);
    const auto seat = runtime.add_participant(first);
    const auto client = runtime.bind(seat, ControllerKind::frontend);
    CHECK_THROWS_AS(runtime.reassign_participant(seat, state::CivilizationSlot{255}),
                    std::invalid_argument);
    CHECK(client.identity().observer == first); // Invalid reassignment did not revoke.
    CHECK_THROWS_AS(runtime.bind(seat, static_cast<ControllerKind>(99)), std::invalid_argument);
    CHECK_THROWS_AS(runtime.bind(seat, ControllerKind::frontend, static_cast<AccessClass>(99)),
                    std::invalid_argument);
    CHECK_THROWS_AS(runtime.bind(ParticipantId{999}, ControllerKind::frontend), std::out_of_range);
    runtime.revoke(ControllerId{999}); // Idempotent revocation never grants access.
    CHECK(client.identity().observer == first);
}
