#include "service_callbacks.hpp"
// Independently authored U01 sections 6, 9–11 / Appendix A expectations.
// V01 and transport are supplied children; these cases do not implement them.
#include "unit/deletion.hpp"
#include <catch2/catch_test_macros.hpp>
#include <limits>
#include <map>
#include <string>

using namespace openciv;
using namespace openciv::unit;
namespace {
struct Fixture {
    state::ProcessContext process;
    state::GameState world{process};
    StackBudget budget{100000, 64};
    std::vector<std::string> calls;
    std::function<void()> on_removed, on_refresh, on_destroyed, on_sync;
    DeletionCallbacks services;
    Fixture() {
        world.map.allocate(world::Geometry{8, 6});
        world.map.tile({2, 2}).features = 0x15;
        world.map.tile({2, 2}).owner = 1;
        world.map.tile({2, 2}).fertility = 7;
        civ().military_count = 10;
        civ().units_active[3] = 10;
        civ().units_lost[3] = 7;
        world.unit_totals[1] = 10;
        world.turn_loop_active = true;
        services.removed_unit = [&](world::Position position) {
            calls.push_back("removed:" + std::to_string(position.x) + "," +
                            std::to_string(position.y));
            if (on_removed)
                on_removed();
        };
        services.refresh_home = [&](std::int32_t home) {
            calls.push_back("refresh:" + std::to_string(home));
            if (on_refresh)
                on_refresh();
        };
        services.check_destroyed = [&](std::int32_t owner, std::int32_t killer) {
            calls.push_back("destroyed:" + std::to_string(owner) + "," + std::to_string(killer));
            if (on_destroyed)
                on_destroyed();
        };
        services.synchronize = [&](world::Position position, std::optional<state::CitySlot> home) {
            calls.push_back("sync");
            if (on_sync)
                on_sync();
            calls.push_back("unit:" + std::to_string(position.x) + "," +
                            std::to_string(position.y));
            if (home && static_cast<std::int32_t>(*home) > 0)
                calls.push_back("home:" + std::to_string(static_cast<std::int32_t>(*home)));
            calls.push_back("flush:5000");
        };
    }
    state::Civilization& civ() { return world.civilizations[1]; }
    state::Unit& unit(std::size_t slot) { return world.entities.units.storage()[slot]; }
    state::UnitRule& rule() { return process.rules->definitions.units[3]; }
    void bound(std::size_t end) {
        world.entities.units.set_end(end);
        world.globals.unit_end = static_cast<std::int16_t>(end);
    }
    void live(std::size_t slot) {
        auto& current = unit(slot);
        current.token = state::UnitToken{static_cast<std::uint32_t>(slot + 1)};
        current.type = state::UnitType{3};
        current.owner = state::CivilizationSlot{1};
        current.x = current.y = 2;
        bound(std::max(slot + 1, world.entities.units.end()));
    }
    UnitDeletion operation() {
        return UnitDeletion(world, process.rules->definitions, budget, {}, services);
    }
};
} // namespace

TEST_CASE("U01 basic deletion performs bookkeeping without loss accounting", "[unit][deletion]") {
    Fixture f;
    f.live(1);
    f.bound(4);
    auto& unit = f.unit(1);
    unit.flags = 0x5703;
    unit.orders = state::UnitOrder::fortified;
    unit.home = state::CitySlot{200};
    unit.moves_used = 33;
    unit.damage = 9;
    unit.destination_x = 57;
    unit.destination_y = -12;
    auto expected = unit;
    expected.token = state::UnitToken{0};
    expected.x = expected.y = -200;
    const auto globals = f.world.globals;
    f.operation().erase(1, false);
    CHECK(unit == expected);
    CHECK(f.world.globals == globals);
    CHECK(f.world.entities.units.end() == 4);
    CHECK(f.civ().military_count == 9);
    CHECK(f.civ().units_active[3] == 9);
    CHECK(f.civ().units_lost[3] == 7);
    CHECK(f.world.unit_totals[1] == 9);
    CHECK(f.world.map.tile({2, 2}).features == 0x14);
    CHECK(f.world.map.tile({2, 2}).owner == 1);
    CHECK(f.world.map.tile({2, 2}).fertility == 7);
    CHECK_FALSE(f.world.unit_deletion.active);
    CHECK_FALSE(f.world.unit_stacks.detach_active);
    CHECK(f.calls.empty());
}

TEST_CASE("U01 deletion counter guards preserve zeros and signed wrap", "[unit][deletion]") {
    Fixture f;
    f.live(0);
    SECTION("zero guards") {
        f.civ().military_count = 0;
        f.civ().units_active[3] = 0;
        f.world.unit_totals[1] = 0;
        f.operation().erase(0, false);
        CHECK(f.civ().military_count == 0);
        CHECK(f.civ().units_active[3] == 0);
        CHECK(f.world.unit_totals[1] == 0);
    }
    SECTION("negative counts and signed rule role") {
        f.civ().military_count = std::numeric_limits<std::int16_t>::min();
        f.civ().units_active[3] = 255;
        f.world.unit_totals[1] = std::numeric_limits<std::int32_t>::min();
        f.rule().role = static_cast<state::RuleRole>(255);
        f.operation().erase(0, false);
        CHECK(f.civ().military_count == 32767);
        CHECK(f.civ().units_active[3] == 254);
        CHECK(f.world.unit_totals[1] == 2147483647);
    }
    SECTION("nonmilitary role") {
        f.rule().role = static_cast<state::RuleRole>(7);
        f.operation().erase(0, false);
        CHECK(f.civ().military_count == 10);
        CHECK(f.civ().units_active[3] == 9);
        CHECK(f.world.unit_totals[1] == 9);
    }
}

TEST_CASE("U01 deletion shrinks only the exact tail once and admits retained backing",
          "[unit][deletion]") {
    Fixture f;
    for (unsigned i = 0; i < 3; ++i)
        f.live(i);
    auto deletion = f.operation();
    SECTION("ascending deletion leaves earlier holes") {
        deletion.erase(0, false);
        CHECK(f.world.entities.units.end() == 3);
        deletion.erase(1, false);
        CHECK(f.world.entities.units.end() == 3);
        deletion.erase(2, false);
        CHECK(f.world.entities.units.end() == 2);
        CHECK(f.world.globals.unit_end == 2);
    }
    SECTION("live record outside current scan is still deleted") {
        f.bound(1);
        deletion.erase(2, false);
        CHECK(f.unit(2).token == state::UnitToken{0});
        CHECK(f.world.entities.units.end() == 1);
        CHECK(f.world.globals.unit_end == 1);
    }
}

TEST_CASE("U01 deletion wakes all scanned sleepers without clearing carrier or aboard fields",
          "[unit][deletion]") {
    Fixture f;
    f.live(1);
    f.bound(5);
    for (unsigned i = 0; i < 6; ++i) {
        f.unit(i).orders = state::UnitOrder::sleeping;
        f.unit(i).destination_x = 1;
        f.unit(i).destination_y = 59;
        f.unit(i).flags = 0x1000;
    }
    f.unit(2).destination_x = 3;
    f.unit(3).orders = state::UnitOrder::fortified;
    f.operation().erase(1, false);
    for (unsigned i : {0U, 1U, 4U}) {
        CHECK(f.unit(i).orders == state::UnitOrder::none);
        CHECK(f.unit(i).destination_x == 1);
        CHECK(f.unit(i).destination_y == 59);
        CHECK(f.unit(i).flags == 0x1000);
    }
    CHECK(f.unit(2).orders == state::UnitOrder::sleeping);
    CHECK(f.unit(3).orders == state::UnitOrder::fortified);
    CHECK(f.unit(5).orders == state::UnitOrder::sleeping); // Outside live bound.
}

TEST_CASE("U01 deletion early returns precede activity authority and backing effects",
          "[unit][deletion]") {
    Fixture f;
    f.world.globals.multiplayer = state::GameMode::tcp;
    f.world.unit_deletion.active = true;
    f.world.unit_deletion.reply = 83;
    f.unit(0).next.value = 3000;
    auto deletion = f.operation();
    deletion.erase(-8, true);
    deletion.erase(0, true);
    CHECK(f.unit(0).next.value == 3000);
    CHECK(f.world.unit_deletion.active);
    CHECK(f.world.unit_deletion.reply == 83);
    CHECK(f.calls.empty());
    f.world.unit_deletion.active = false;
    CHECK_THROWS_AS(deletion.erase(3000, true), std::out_of_range);
    CHECK_FALSE(f.world.unit_deletion.active); // The lifetime read is before activity.
    CHECK(f.calls.empty());
}

TEST_CASE("U01 deletion notifications distinguish home zero from positive home",
          "[unit][deletion]") {
    Fixture f;
    f.live(0);
    f.world.globals.multiplayer = state::GameMode::tcp;
    f.world.multiplayer.network_host = true;
    SECTION("home zero") {
        f.unit(0).home = state::CitySlot{0};
        f.operation().erase(0, true);
        CHECK(f.calls == std::vector<std::string>{"removed:2,2", "refresh:0", "sync", "unit:2,2",
                                                  "flush:5000"});
    }
    SECTION("unsigned high home") {
        f.unit(0).home = state::CitySlot{200};
        f.operation().erase(0, true);
        CHECK(f.calls == std::vector<std::string>{"removed:2,2", "refresh:200", "sync", "unit:2,2",
                                                  "home:200", "flush:5000"});
    }
    SECTION("absent home") {
        f.operation().erase(0, true);
        CHECK(f.calls == std::vector<std::string>{"removed:2,2", "sync", "unit:2,2", "flush:5000"});
    }
    CHECK_FALSE(f.world.unit_deletion.active);
}

TEST_CASE("U01 deletion captures selectors but reads elimination rules counters and mode live",
          "[unit][deletion]") {
    Fixture f;
    f.live(0);
    f.unit(0).home = state::CitySlot{200};
    f.on_removed = [&] {
        CHECK(f.world.unit_deletion.active);
        CHECK(f.unit(0).token == state::UnitToken{0});
        f.unit(0).owner = state::CivilizationSlot{2};
        f.unit(0).type = state::UnitType{4};
        f.unit(0).home = state::CitySlot{7};
        f.unit(0).x = f.unit(0).y = 4;
        f.rule().role = static_cast<state::RuleRole>(5);
        f.civ().city_count = 0;
        f.civ().units_active[3] = 0;
    };
    f.on_destroyed = [&] { f.world.globals.multiplayer = state::GameMode::tcp; };
    f.on_sync = [&] { f.world.globals.multiplayer = state::GameMode::single_player; };
    f.operation().erase(0, true);
    CHECK(f.calls == std::vector<std::string>{"removed:2,2", "refresh:200", "destroyed:1,-1",
                                              "sync", "unit:2,2", "home:200", "flush:5000"});
    CHECK_FALSE(f.world.unit_deletion.active);
}

TEST_CASE("U01 last exact settlement type invokes destruction check even without notification",
          "[unit][deletion]") {
    Fixture f;
    f.live(0);
    f.rule().role = static_cast<state::RuleRole>(5);
    f.civ().units_active[3] = 1;
    f.civ().units_active[4] = 5;
    f.process.rules->definitions.units[4].role = static_cast<state::RuleRole>(5);
    f.world.globals.civs_alive = 0xff;
    SECTION("zero cities calls V01 despite other settlement type") {
        f.operation().erase(0, false);
        CHECK(f.calls == std::vector<std::string>{"destroyed:1,-1"});
        CHECK(f.world.globals.civs_alive == 0xff); // Supplied child does not eliminate.
    }
    SECTION("positive cities suppress check") {
        f.civ().city_count = 1;
        f.operation().erase(0, false);
        CHECK(f.calls.empty());
    }
    SECTION("another unit of the exact type suppresses check") {
        f.civ().units_active[3] = 2;
        f.operation().erase(0, false);
        CHECK(f.calls.empty());
    }
    CHECK(f.civ().military_count == 10);
}

TEST_CASE("U01 interrupted deletion retains counters activity and established publications",
          "[unit][deletion]") {
    Fixture f;
    f.live(0);
    SECTION("unbacked detach") {
        f.unit(0).next.value = 3000;
        CHECK_THROWS_AS(f.operation().erase(0, false), std::out_of_range);
        CHECK(f.unit(0).token == state::UnitToken{1});
        CHECK(f.world.entities.units.end() == 1);
        CHECK(f.world.unit_stacks.detach_active);
    }
    SECTION("missing elimination service") {
        f.rule().role = static_cast<state::RuleRole>(5);
        f.civ().units_active[3] = 1;
        f.services.check_destroyed = {};
        CHECK_THROWS_AS(f.operation().erase(0, false), std::bad_function_call);
        CHECK(f.unit(0).token == state::UnitToken{0});
        CHECK(f.world.entities.units.end() == 0);
        CHECK_FALSE(f.world.unit_stacks.detach_active);
    }
    SECTION("interrupted local notice") {
        f.unit(0).home = state::CitySlot{1};
        f.on_removed = [] { throw std::runtime_error("notice stopped"); };
        CHECK_THROWS_AS(f.operation().erase(0, true), std::runtime_error);
        CHECK(f.calls == std::vector<std::string>{"removed:2,2"});
        CHECK(f.unit(0).token == state::UnitToken{0});
        CHECK(f.world.entities.units.end() == 0);
    }
    CHECK(f.world.unit_totals[1] == 9);
    CHECK(f.world.unit_deletion.active);
}

TEST_CASE("U01 deletion activity is a boolean assignment across reentrant children",
          "[unit][deletion]") {
    Fixture f;
    f.live(0);
    f.live(1);
    auto deletion = f.operation();
    f.on_removed = [&] {
        CHECK(f.world.unit_deletion.active);
        deletion.erase(1, false);
        CHECK_FALSE(f.world.unit_deletion.active);
        throw std::runtime_error("outer interrupted after child");
    };
    CHECK_THROWS_AS(deletion.erase(0, true), std::runtime_error);
    CHECK_FALSE(f.world.unit_deletion.active);
    CHECK(f.unit(0).token == state::UnitToken{0});
    CHECK(f.unit(1).token == state::UnitToken{0});
    CHECK(f.world.entities.units.end() == 1);
}

TEST_CASE("U01 deletion rejects out-of-pool slots and retains signed owner meaning",
          "[unit][deletion]") {
    Fixture f;
    SECTION("out-of-pool slot") {
        f.live(0);
        CHECK_THROWS_AS(f.operation().erase(2048, false), std::out_of_range);
    }
    SECTION("negative owner skips counters and is detached at signed staging") {
        f.live(0);
        f.unit(0).owner = state::CivilizationSlot{255};
        f.operation().erase(0, false);
        CHECK(f.unit(0).x == 0);
        CHECK(f.unit(0).y == 0);
        CHECK(f.world.unit_totals[1] == 10);
    }
    SECTION("negative settlement owner stops at later civilization read") {
        f.live(0);
        f.unit(0).owner = state::CivilizationSlot{255};
        f.rule().role = static_cast<state::RuleRole>(5);
        CHECK_THROWS_AS(f.operation().erase(0, false), std::out_of_range);
        CHECK(f.unit(0).token == state::UnitToken{0});
        CHECK(f.world.unit_deletion.active);
    }
}
