// Independently authored U01 sections 2–4, 9–11 placement/authority cases.
// Incoming messages are supplied effects, not original-peer execution evidence.
#include "unit/stack_operations.hpp"
#include <catch2/catch_test_macros.hpp>
#include <map>
#include <string>

using namespace openciv;
using namespace openciv::unit;
namespace {
struct Fixture {
    state::ProcessContext process;
    state::GameState world{process};
    StackBudget budget{100000, 64};
    StackOperationServices services;
    std::vector<std::string> calls;
    std::vector<StackDiagnostic> reports;
    std::function<void()> on_sync, on_flush;
    Fixture() {
        world.map.allocate(world::Geometry{8, 6});
        world.multiplayer.network_host = true;
        world.globals.multiplayer = state::GameMode::tcp;
        world.turn_loop_active = true;
        services.synchronize = [&] {
            calls.push_back("sync");
            if (on_sync)
                on_sync();
            calls.push_back("flush");
            if (on_flush)
                on_flush();
        };
    }
    state::Unit& unit(std::size_t index) { return world.entities.units.storage()[index]; }
    void live(unsigned index, world::Position position = {2, 2}) {
        unit(index).token = state::UnitToken{index + 1};
        unit(index).owner = state::CivilizationSlot{1};
        unit(index).x = static_cast<std::int16_t>(position.x);
        unit(index).y = static_cast<std::int16_t>(position.y);
        world.entities.units.set_end(std::max(world.entities.units.end(), std::size_t{index + 1}));
        world.globals.unit_end = static_cast<std::int16_t>(world.entities.units.end());
    }
    void chain(std::initializer_list<unsigned> members, world::Position position = {2, 2}) {
        std::int16_t previous = -1;
        for (auto slot : members) {
            live(slot, position);
            unit(slot).previous.value = previous;
            if (previous >= 0)
                unit(static_cast<std::size_t>(previous)).next.value =
                    static_cast<std::int16_t>(slot);
            previous = static_cast<std::int16_t>(slot);
        }
        world.map.tile(position).features = 0x11;
        world.map.tile(position).owner = 1;
    }
    bool& active(PlacementOperation operation) {
        switch (operation) {
        case PlacementOperation::detach:
            return world.unit_stacks.detach_active;
        case PlacementOperation::attach:
            return world.unit_stacks.attach_active;
        case PlacementOperation::relocate:
            return world.unit_stacks.relocate_active;
        case PlacementOperation::bottom:
            return world.unit_stacks.bottom_active;
        }
        throw std::logic_error("bad test selector");
    }
    std::int32_t& reply(PlacementOperation operation) {
        switch (operation) {
        case PlacementOperation::detach:
            return world.unit_stacks.detach_reply;
        case PlacementOperation::attach:
            return world.unit_stacks.attach_reply;
        case PlacementOperation::relocate:
            return world.unit_stacks.relocate_reply;
        case PlacementOperation::bottom:
            return world.unit_stacks.bottom_reply;
        }
        throw std::logic_error("bad test selector");
    }
    StackOperations operations() { return StackOperations(world, budget, {&reports}, services); }
    std::vector<std::int32_t> order(std::int32_t head) {
        std::vector<std::int32_t> result;
        for (auto current = head; current >= 0 && result.size() < 20;
             current = unit(static_cast<std::size_t>(current)).next.value)
            result.push_back(current);
        return result;
    }
};
} // namespace

TEST_CASE("U01 relocate in place moves a middle member to front and synchronizes only its tail",
          "[unit][placement]") {
    Fixture f;
    f.chain({4, 2, 7});
    f.unit(2).moves_used = 55;
    f.unit(2).home = state::CitySlot{200};
    f.unit(2).orders = state::UnitOrder::sleeping;
    const auto tile = f.world.map.tile({2, 2});
    f.on_sync = [&] {
        CHECK(f.world.unit_stacks.relocate_active);
        CHECK_FALSE(f.world.unit_stacks.detach_active);
        CHECK_FALSE(f.world.unit_stacks.attach_active);
        f.world.globals.multiplayer = state::GameMode::single_player;
    };
    f.on_flush = [&] { CHECK(f.world.unit_stacks.relocate_active); };
    f.operations().relocate_in_place(2, true);
    CHECK(f.order(2) == std::vector<std::int32_t>{2, 4, 7});
    CHECK(f.unit(2).previous.value == -1);
    CHECK(f.unit(4).previous.value == 2);
    CHECK(f.unit(7).previous.value == 4);
    CHECK(f.unit(2).moves_used == 55);
    CHECK(f.unit(2).home == state::CitySlot{200});
    CHECK(f.unit(2).orders == state::UnitOrder::sleeping);
    CHECK(f.world.map.tile({2, 2}) == tile);
    CHECK(f.calls == std::vector<std::string>{"sync", "flush"});
    CHECK_FALSE(f.world.unit_stacks.relocate_active);
}

TEST_CASE("U01 direct placement keeps activity through synchronization and flush",
          "[unit][placement]") {
    Fixture f;
    f.live(0);
    PlacementOperation operation;
    SECTION("detach") {
        operation = PlacementOperation::detach;
    }
    SECTION("attach") {
        operation = PlacementOperation::attach;
        f.unit(0).x = f.unit(0).y = -200;
    }
    f.on_sync = [&] {
        CHECK(f.active(operation));
        f.world.globals.multiplayer = state::GameMode::single_player;
    };
    f.on_flush = [&] { CHECK(f.active(operation)); };
    auto stacks = f.operations();
    if (operation == PlacementOperation::detach)
        stacks.detach(0, true);
    else
        stacks.attach_at_front(0, {2, 2}, true);
    CHECK_FALSE(f.active(operation));
    CHECK(f.calls == std::vector<std::string>{"sync", "flush"});
}

TEST_CASE("U01 bottom operation preserves order and its terminal guard precedes authority",
          "[unit][placement]") {
    Fixture f;
    f.chain({4, 2, 7});
    auto stacks = f.operations();
    SECTION("head") {
        stacks.move_to_bottom(4, true);
        CHECK(f.order(2) == std::vector<std::int32_t>{2, 7, 4});
        CHECK(f.unit(2).previous.value == -1);
        CHECK(f.unit(7).previous.value == 2);
        CHECK(f.unit(4).previous.value == 7);
        CHECK(f.calls == std::vector<std::string>{"sync", "flush"});
    }
    SECTION("middle") {
        stacks.move_to_bottom(2, false);
        CHECK(f.order(4) == std::vector<std::int32_t>{4, 7, 2});
        CHECK(f.calls.empty());
    }
    SECTION("already last on client") {
        f.world.multiplayer.network_host = false;
        f.world.unit_stacks.bottom_active = true;
        f.world.unit_stacks.bottom_reply = 35;
        stacks.move_to_bottom(7, true);
        CHECK(f.order(4) == std::vector<std::int32_t>{4, 2, 7});
        CHECK(f.world.unit_stacks.bottom_active);
        CHECK(f.world.unit_stacks.bottom_reply == 35);
        CHECK(f.calls.empty());
    }
    for (auto slot : {4U, 2U, 7U}) {
        CHECK(f.unit(slot).x == 2);
        CHECK(f.unit(slot).y == 2);
    }
}

TEST_CASE("U01 whole-stack relocation reverses transferred members with one outer synchronization",
          "[unit][placement]") {
    Fixture f;
    f.chain({4, 2, 7});
    auto stacks = f.operations();
    SECTION("another stack") {
        f.chain({8, 9}, {6, 2});
        stacks.relocate_stack(2, {6, 2}, true);
        CHECK(f.order(7) == std::vector<std::int32_t>{7, 2, 4, 8, 9});
        CHECK(f.unit(8).previous.value == 4);
        CHECK(f.world.map.tile({2, 2}).features == 0x10);
        CHECK(f.world.map.tile({2, 2}).owner == 1);
        CHECK(f.unit(4).x == 6);
        CHECK(f.unit(2).x == 6);
        CHECK(f.unit(7).x == 6);
    }
    SECTION("same position") {
        stacks.relocate_stack(2, {2, 2}, true);
        CHECK(f.order(7) == std::vector<std::int32_t>{7, 2, 4});
    }
    CHECK(f.calls == std::vector<std::string>{"sync", "flush"});
    CHECK_FALSE(f.world.unit_stacks.relocate_active);
}

TEST_CASE("U01 empty whole-stack relocation still runs synchronization without an activity gate",
          "[unit][placement]") {
    Fixture f;
    f.world.multiplayer.network_host = false;
    f.world.unit_stacks.relocate_active = true;
    f.world.unit_stacks.relocate_reply = 19;
    f.budget.steps = 0;
    f.operations().relocate_stack(-7, {6, 2}, true);
    CHECK(f.calls == std::vector<std::string>{"sync", "flush"});
    CHECK(f.world.unit_stacks.relocate_active);
    CHECK(f.world.unit_stacks.relocate_reply == 19);
}

TEST_CASE("U01 placement failures retain local mutations and nested activity markers",
          "[unit][placement]") {
    Fixture f;
    f.chain({0});
    SECTION("sync stops after detach") {
        f.on_sync = [] { throw std::runtime_error("sync stopped"); };
        CHECK_THROWS_AS(f.operations().detach(0, true), std::runtime_error);
        CHECK(f.unit(0).x == -200);
        CHECK(f.world.map.tile({2, 2}).features == 0x10);
        CHECK(f.world.unit_stacks.detach_active);
        CHECK(f.calls == std::vector<std::string>{"sync"});
    }
}

TEST_CASE("U01 repair diagnostics preserve host authority", "[unit][placement]") {
    Fixture f;
    f.live(0);
    f.unit(0).previous.value = f.unit(0).next.value = 0;
    CHECK(f.operations().validate(0) == 0);
    CHECK(f.world.multiplayer.network_host);
    CHECK(f.unit(0).previous.value == -1);
    CHECK(f.unit(0).next.value == -1);
    CHECK(f.world.map.tile({2, 2}).unit());
}
