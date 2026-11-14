// F06 section 11 step 5, composed with real U01 sections 3/6 mechanics.
// Independently authored state; supplied V01 child effects are not V01 evidence.
#include "session/classic_loaded_units.hpp"
#include <catch2/catch_test_macros.hpp>

using namespace openciv;
namespace {
struct Fixture {
    state::ProcessContext process;
    state::GameState world{process};
    unit::StackBudget budget{100000, 64};
    std::vector<unit::StackDiagnostic> reports;
    std::vector<std::pair<std::int32_t, std::int32_t>> checks;
    std::function<void(unit::StackDiagnostic)> report_action;
    std::function<void()> check_action;
    unit::DeletionServices deletion;
    unit::StackOperationServices placement;
    Fixture() {
        world.map.allocate(world::Geometry{8, 6});
        world.globals.multiplayer = 3;
        world.civilizations[1].military_count = 3;
        world.civilizations[1].units_active[3] = 3;
        world.unit_totals[1] = 3;
        deletion.check_destroyed = [&](std::int32_t owner, std::int32_t killer) {
            checks.emplace_back(owner, killer);
            if (check_action)
                check_action();
        };
        // All notification/request services remain unbound: using them would
        // stop this actual composition instead of fabricating success.
    }
    state::Unit& unit(std::size_t slot) { return world.entities.units.storage()[slot]; }
    void bound(std::size_t end) {
        world.entities.units.set_end(end);
        world.globals.unit_end = static_cast<std::int16_t>(end);
    }
    void live(std::size_t slot, world::Position position) {
        unit(slot).token = state::UnitToken{static_cast<std::uint32_t>(slot + 1)};
        unit(slot).x = static_cast<std::int16_t>(position.x);
        unit(slot).y = static_cast<std::int16_t>(position.y);
        unit(slot).owner = state::CivilizationSlot{1};
        unit(slot).type = state::UnitType{3};
        bound(std::max(slot + 1, world.entities.units.end()));
    }
    void cleanup() {
        session::cleanup_classic_loaded_units(world, process.rules->definitions, budget,
                                              {[&](unit::StackDiagnostic event) {
                                                   if (reports.empty())
                                                       CHECK(world.multiplayer.network_host);
                                                   reports.push_back(event);
                                                   if (report_action)
                                                       report_action(event);
                                               },
                                               {}},
                                              placement, deletion);
    }
};
} // namespace

TEST_CASE("F06 repair routes placement after a diagnostic changes host authority",
          "[session][load][unit][placement]") {
    Fixture f;
    f.live(0, {2, 2});
    f.unit(0).previous.value = f.unit(0).next.value = 0;
    std::vector<unit::PlacementRequest> requests;
    std::optional<unit::PlacementOperation> pending;
    std::uint32_t tick = 0;
    f.report_action = [&](unit::StackDiagnostic event) {
        CHECK(event.problem == unit::StackProblem::infinite);
        f.world.multiplayer.network_host = false;
    };
    f.placement.request = [&](unit::PlacementRequest request) {
        requests.push_back(request);
        pending = request.operation;
    };
    f.placement.ticks = [&] { return ++tick; };
    f.placement.priority_messages = [] { return false; };
    f.placement.normal_messages = [] { return false; };
    f.placement.process_messages = [&](std::int32_t first, std::int32_t second) {
        CHECK(first == 1);
        CHECK(second == 1);
        REQUIRE(pending);
        // Explicit incoming state/replies, not a locally executed host helper.
        // Distinct positions make any extra local placement observable.
        if (*pending == unit::PlacementOperation::detach) {
            CHECK_FALSE(f.world.unit_stacks.detach_active);
            f.unit(0).x = -321;
            f.unit(0).y = -322;
            f.unit(0).previous.value = f.unit(0).next.value = -1;
            f.world.unit_stacks.detach_reply = 17;
            f.world.unit_stacks.detach_active = true;
        } else {
            CHECK(*pending == unit::PlacementOperation::attach);
            CHECK_FALSE(f.world.unit_stacks.attach_active);
            f.unit(0).x = f.unit(0).y = 4;
            f.world.unit_stacks.attach_reply = 19;
            f.world.unit_stacks.attach_active = true;
        }
        pending.reset();
    };
    f.cleanup();
    CHECK(requests ==
          std::vector<unit::PlacementRequest>{{unit::PlacementOperation::detach, {0}},
                                              {unit::PlacementOperation::attach, {0, 2, 2}}});
    CHECK(f.unit(0).x == 4);
    CHECK(f.unit(0).y == 4);
    CHECK(f.unit(0).token == state::UnitToken{1});
    CHECK_FALSE(f.world.multiplayer.network_host);
    CHECK(f.world.unit_stacks.detach_active);
    CHECK(f.world.unit_stacks.attach_active);
    CHECK(f.world.unit_totals[1] == 3);
    CHECK(f.world.entities.units.end() == 1);
}

TEST_CASE("F06 post-load scan establishes host then repairs and deletes in slot order",
          "[session][load][unit]") {
    Fixture f;
    f.unit(0).previous.value = 3000;
    f.unit(0).next.value = 3001;
    f.live(1, {-2, 2});
    f.unit(1).next.value = 2;
    f.live(2, {2, 2});
    f.unit(2).previous.value = 1;
    f.unit(2).orders = state::UnitOrder::sleeping;
    f.unit(2).destination_x = 1;
    f.unit(3).previous.value = 99;
    f.unit(3).next.value = 100;
    f.live(4, {100, 100}); // Positive out-of-map coordinates are not a deletion gate.
    f.live(5, {-20, -20});
    f.bound(5);
    const auto outside = f.unit(5);
    f.report_action = [&](unit::StackDiagnostic event) {
        CHECK(event == unit::StackDiagnostic{unit::StackProblem::forward_x, 1, 2});
        CHECK(f.unit(0).previous.value == -1);
        CHECK(f.unit(0).next.value == -1);
        CHECK(f.unit(3).previous.value == 99);
    };
    f.cleanup();
    CHECK(f.world.multiplayer.network_host);
    CHECK(f.reports.size() == 1);
    CHECK(f.unit(1).token == state::UnitToken{0});
    CHECK(f.unit(1).x == -200);
    CHECK(f.unit(2).previous.value == -1);
    CHECK(f.unit(2).orders == state::UnitOrder::none);
    CHECK(f.unit(2).destination_x == 1);
    CHECK(f.unit(3).previous.value == -1);
    CHECK(f.unit(3).next.value == -1);
    CHECK(f.unit(4).token == state::UnitToken{5});
    CHECK(f.unit(4).x == 100);
    CHECK(f.unit(5) == outside);
    CHECK(f.world.entities.units.end() == 5);
    CHECK(f.world.globals.unit_end == 5);
    CHECK(f.world.civilizations[1].military_count == 2);
    CHECK(f.world.civilizations[1].units_active[3] == 2);
    CHECK(f.world.unit_totals[1] == 2);
    CHECK(f.checks.empty());
}

TEST_CASE("F06 post-load negative coordinate gate uses either component after validation",
          "[session][load][unit]") {
    Fixture f;
    SECTION("negative x only") {
        f.live(0, {-1, 2});
    }
    SECTION("negative y only") {
        f.live(0, {2, -1});
    }
    SECTION("validation callback changes coordinates") {
        f.live(0, {2, 2});
        f.live(1, {4, 4});
        f.unit(0).next.value = 1;
        f.unit(1).previous.value = 0;
        f.report_action = [&](unit::StackDiagnostic) { f.unit(0).x = -1; };
    }
    f.cleanup();
    CHECK(f.unit(0).token == state::UnitToken{0});
    CHECK(f.world.unit_totals[1] == 2);
}

TEST_CASE("F06 post-load self-loop cleanup uses real U01 repair without deleting the unit",
          "[session][load][unit]") {
    Fixture f;
    f.live(0, {2, 2});
    f.unit(0).previous.value = f.unit(0).next.value = 0;
    f.cleanup();
    CHECK(f.reports == std::vector<unit::StackDiagnostic>{{unit::StackProblem::infinite, 0}});
    CHECK(f.unit(0).token == state::UnitToken{1});
    CHECK(f.unit(0).previous.value == -1);
    CHECK(f.unit(0).next.value == -1);
    CHECK(f.world.map.tile({2, 2}).unit());
    CHECK(f.world.entities.units.end() == 1);
    CHECK(f.world.unit_totals[1] == 3);
}

TEST_CASE("F06 post-load destruction child can extend the live scan after tail deletion",
          "[session][load][unit]") {
    Fixture f;
    f.live(0, {-2, 2});
    f.process.rules->definitions.units[3].role = state::RuleRole::settle;
    f.world.civilizations[1].units_active[3] = 1;
    f.check_action = [&] {
        CHECK(f.unit(0).token == state::UnitToken{0});
        CHECK(f.world.entities.units.end() == 0);
        CHECK(f.world.unit_deletion.active);
        f.unit(1).previous.value = 67;
        f.unit(1).next.value = 68;
        f.live(2, {2, -2});
        f.unit(2).type = state::UnitType{4};
        f.world.civilizations[1].units_active[4] = 1;
    };
    f.cleanup();
    CHECK(f.checks == std::vector<std::pair<std::int32_t, std::int32_t>>{{1, -1}});
    CHECK(f.unit(1).previous.value == -1);
    CHECK(f.unit(1).next.value == -1);
    CHECK(f.unit(2).token == state::UnitToken{0});
    CHECK(f.world.entities.units.end() == 2);
    CHECK(f.world.globals.unit_end == 2);
    CHECK(f.world.civilizations[1].units_active[4] == 0);
    CHECK(f.world.unit_totals[1] == 1);
}

TEST_CASE("F06 interrupted post-load repair retains host and earlier slot cleanup",
          "[session][load][unit]") {
    Fixture f;
    f.unit(0).previous.value = 89;
    f.live(1, {2, 2});
    f.live(2, {4, 4});
    f.unit(1).next.value = 2;
    f.unit(2).previous.value = 1;
    f.unit(3).previous.value = 88;
    f.bound(4);
    f.report_action = [](unit::StackDiagnostic) { throw std::runtime_error("stop"); };
    CHECK_THROWS_AS(f.cleanup(), std::runtime_error);
    CHECK(f.world.multiplayer.network_host);
    CHECK(f.unit(0).previous.value == -1);
    CHECK(f.unit(1).next.value == 2);
    CHECK(f.unit(2).previous.value == 1);
    CHECK(f.unit(3).previous.value == 88);
    CHECK(f.world.entities.units.end() == 4);
}
