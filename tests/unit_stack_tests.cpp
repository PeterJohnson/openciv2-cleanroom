// Independently authored expectations from U01 sections 2–4, 9–11 and Appendix A.
#include "unit/stacks.hpp"
#include <catch2/catch_test_macros.hpp>
#include <map>

using namespace openciv;
using namespace openciv::unit;
namespace {
struct Fixture {
    state::UnitPool units;
    world::Tile fallback;
    world::MapStorage map{fallback};
    state::UnitStackState retained;
    StackBudget budget{50000000, 64};
    std::vector<StackDiagnostic> reports;
    std::map<std::int32_t, state::Unit> extra;
    std::vector<std::int32_t> extra_reads;
    std::function<void(StackDiagnostic)> on_report;
    LocalStacks stacks{units,
                       map,
                       retained,
                       budget,
                       {[&](StackDiagnostic event) {
                            reports.push_back(event);
                            if (on_report)
                                on_report(event);
                        },
                        [&](std::int32_t slot) -> state::Unit* {
                            extra_reads.push_back(slot);
                            const auto found = extra.find(slot);
                            return found == extra.end() ? nullptr : &found->second;
                        }}};
    Fixture() { map.allocate(world::Geometry{8, 6}); }
    state::Unit& unit(std::size_t slot) { return units.storage()[slot]; }
    void live(std::size_t slot, world::Position p = {2, 2}, unsigned owner = 1) {
        unit(slot).token = state::UnitToken{static_cast<std::uint32_t>(slot + 1)};
        unit(slot).x = static_cast<std::int16_t>(p.x);
        unit(slot).y = static_cast<std::int16_t>(p.y);
        unit(slot).owner = static_cast<state::CivilizationSlot>(owner);
        units.set_end(std::max(units.end(), slot + 1));
    }
    void chain(std::initializer_list<unsigned> members) {
        std::int16_t previous = -1;
        for (auto slot : members) {
            live(slot);
            unit(slot).previous.value = previous;
            if (previous >= 0)
                unit(static_cast<std::size_t>(previous)).next.value =
                    static_cast<std::int16_t>(slot);
            previous = static_cast<std::int16_t>(slot);
        }
    }
};
} // namespace

TEST_CASE("U01 stack accessors preserve order and negative terminals", "[unit][stack]") {
    Fixture f;
    f.chain({4, 2, 7});
    f.unit(4).previous.value = -8;
    f.unit(7).next.value = -12;
    const std::vector<state::Unit> before(f.units.storage().begin(), f.units.storage().end());
    CHECK(f.stacks.raw_forward_count(2) == 2);
    CHECK(f.stacks.validate(2) == 1);
    CHECK(f.stacks.first(7) == 4);
    CHECK(f.stacks.last(4) == 7);
    CHECK(f.stacks.next(7) == -12);
    CHECK(f.stacks.first_for_owner_at(1, {2, 2}) == 4); // Lowest match is slot 2.
    CHECK(f.stacks.first_for_owner_at(2, {2, 2}) == -1);
    CHECK(f.stacks.first_for_owner_at(1, {3, 2}) == -1); // W01 aliases this tile.
    CHECK(f.stacks.next(-19) == -19);
    CHECK(f.stacks.first(-19) == -19);
    CHECK(f.stacks.last(-19) == -19);
    CHECK(f.stacks.raw_forward_count(-19) == 0);
    CHECK(std::equal(before.begin(), before.end(), f.units.storage().begin()));
    CHECK(f.reports.empty());
}

TEST_CASE("U01 validation does not impose liveness reciprocity or ownership", "[unit][stack]") {
    Fixture f;
    f.chain({0, 1});
    SECTION("dead terminal") {
        f.unit(1).token = state::UnitToken{0};
    }
    SECTION("nonreciprocal") {
        f.unit(1).previous.value = -1;
    }
    SECTION("different owner") {
        f.unit(1).owner = state::CivilizationSlot{5};
    }
    const auto first = f.unit(0), second = f.unit(1);
    CHECK(f.stacks.validate(0) == 1);
    CHECK(f.stacks.validate(1) == 1);
    CHECK(f.unit(0) == first);
    CHECK(f.unit(1) == second);
    CHECK(f.reports.empty());
}

TEST_CASE("U01 forward dead pass tests current and retains its incoming link", "[unit][stack]") {
    Fixture f;
    f.chain({0, 1, 2});
    f.unit(1).token = state::UnitToken{0};
    CHECK(f.stacks.raw_forward_count(0) == 3);
    CHECK(f.stacks.validate(0) == 0);
    CHECK(f.reports == std::vector<StackDiagnostic>{{StackProblem::forward_dead, 1, 2}});
    CHECK(f.unit(0).next.value == 1);
    CHECK(f.unit(1).previous.value == 0);
    CHECK(f.unit(1).next.value == -1);
    CHECK(f.unit(2).previous.value == -1);
}

TEST_CASE("U01 backward repair follows saved neighbors and live reverse links", "[unit][stack]") {
    Fixture f;
    f.chain({0, 1, 2});
    f.unit(0).token = f.unit(1).token = state::UnitToken{0};
    f.on_report = [&](StackDiagnostic event) {
        if (event.current == 2) {
            f.unit(2).previous.value = 0;
            f.unit(1).next.value = 7;
        }
    };
    CHECK(f.stacks.validate(2) == 0);
    CHECK(f.reports == std::vector<StackDiagnostic>{{StackProblem::backward_dead, 2, 1},
                                                    {StackProblem::backward_dead, 1, 0}});
    CHECK(f.unit(2).previous.value == -1);
    CHECK(f.unit(1).previous.value == -1);
    CHECK(f.unit(1).next.value == 7); // Diagnostic changed the reverse before its test.
    CHECK(f.unit(0).next.value == -1);
}

TEST_CASE("U01 position passes continue beyond cuts and compare x before y", "[unit][stack]") {
    Fixture f;
    f.chain({0, 1, 2});
    f.unit(1).x = f.unit(1).y = 4;
    SECTION("forward") {
        CHECK(f.stacks.validate(0) == 0);
        CHECK(f.reports == std::vector<StackDiagnostic>{{StackProblem::forward_x, 0, 1},
                                                        {StackProblem::forward_x, 1, 2}});
    }
    SECTION("backward") {
        CHECK(f.stacks.validate(2) == 0);
        CHECK(f.reports == std::vector<StackDiagnostic>{{StackProblem::backward_x, 2, 1},
                                                        {StackProblem::backward_x, 1, 0}});
    }
    SECTION("y only") {
        f.unit(1).x = 2;
        CHECK(f.stacks.validate(0) == 0);
        CHECK(f.reports == std::vector<StackDiagnostic>{{StackProblem::forward_y, 0, 1},
                                                        {StackProblem::forward_y, 1, 2}});
    }
    for (unsigned i = 0; i < 3; ++i) {
        CHECK(f.unit(i).previous.value == -1);
        CHECK(f.unit(i).next.value == -1);
    }
}

TEST_CASE("U01 failed diagnostics preserve earlier cuts without cutting the current edge",
          "[unit][stack]") {
    Fixture f;
    f.chain({0, 1, 2});
    f.unit(0).token = f.unit(1).token = state::UnitToken{0};
    f.on_report = [&](StackDiagnostic) {
        if (f.reports.size() == 2)
            throw std::runtime_error("diagnostic stopped");
    };
    CHECK_THROWS_AS(f.stacks.validate(2), std::runtime_error);
    CHECK(f.unit(2).previous.value == -1);
    CHECK(f.unit(1).next.value == -1);
    CHECK(f.unit(1).previous.value == 0);
    CHECK(f.unit(0).next.value == 1);
    CHECK(f.reports.size() == 2);
}

TEST_CASE("U01 assertion precedes suppression and needs backing only after returning",
          "[unit][stack]") {
    Fixture f;
    f.on_report = [&](StackDiagnostic event) {
        if (event.problem == StackProblem::entry_range)
            f.retained.suppression = -7;
    };
    CHECK(f.stacks.validate(-9) == 1);
    CHECK(f.reports == std::vector<StackDiagnostic>{{StackProblem::entry_range, -9}});
    CHECK(f.extra_reads.empty());
    f.on_report = {};
    f.retained.suppression = 0;
    CHECK_THROWS_AS(f.stacks.validate(2048), std::out_of_range);
    CHECK(f.reports.size() == 1); // 2048 and 2049 do not assert.
    f.extra[2048] = state::Unit{};
    CHECK(f.stacks.validate(2048) == 1);
    f.extra[2050] = state::Unit{};
    CHECK(f.stacks.validate(2050) == 1);
    CHECK(f.reports.back() == StackDiagnostic{StackProblem::entry_range, 2050});
    CHECK(f.units.end() == 0); // Access is not restricted to scanned/live records.
}

TEST_CASE("U01 exact tile lookup honors animation and performs both validations", "[unit][stack]") {
    Fixture f;
    f.chain({0, 1});
    f.unit(1).x = 4;
    f.unit(2).x = f.unit(2).y = 2; // Dead, initially unlinked.
    f.on_report = [&](StackDiagnostic event) {
        if (event.problem == StackProblem::forward_x) {
            f.unit(0).previous.value = 2;
            f.unit(2).next.value = 0;
        }
    };
    f.retained.animation_active = true;
    CHECK(f.stacks.first_at({2, 2}) == -1);
    CHECK(f.reports.empty());
    f.map.tile({2, 2}).features = 1;
    f.map.tile({2, 2}).owner = 0;
    CHECK(f.stacks.first_at({3, 2}) == -1);
    CHECK(f.stacks.first_at({2, 2}) == 0);
    CHECK(f.reports == std::vector<StackDiagnostic>{{StackProblem::forward_x, 0, 1},
                                                    {StackProblem::backward_dead, 0, 2}});
    f.live(0, {-200, -200}, 3);
    f.retained.animation_active = false;
    CHECK(f.stacks.first_at({-200, -200}) == 0);
    CHECK(f.stacks.first_for_owner_at(3, {-200, -200}) == 0);
}

TEST_CASE("U01 singleton detach retains tile owner fertility and other unit fields",
          "[unit][stack]") {
    Fixture f;
    f.live(0);
    f.unit(0).flags = 0x407;
    f.unit(0).orders = state::UnitOrder::sleeping;
    f.unit(0).moves_used = 21;
    f.unit(0).home = state::CitySlot{0};
    auto expected = f.unit(0);
    expected.x = expected.y = -200;
    auto& tile = f.map.tile({2, 2});
    tile.features = 0x15;
    tile.owner = 1;
    tile.fertility = 7;
    auto expected_tile = tile;
    expected_tile.features = 0x14;
    f.stacks.detach(0);
    CHECK(f.unit(0) == expected);
    CHECK(tile == expected_tile);
    CHECK_FALSE(f.retained.detach_active);
    CHECK(f.units.end() == 1);
}

TEST_CASE("U01 detached middle unit can be attached at the front without tile publication",
          "[unit][stack]") {
    Fixture f;
    f.chain({4, 2, 7});
    auto& tile = f.map.tile({2, 2});
    tile.features = 0x10;
    tile.owner = 5;
    const auto before = tile;
    f.stacks.detach(2);
    CHECK(f.unit(4).next.value == 7);
    CHECK(f.unit(7).previous.value == 4);
    CHECK(f.unit(2).x == -200);
    CHECK(f.unit(2).previous.value == -1);
    CHECK(f.unit(2).next.value == -1);
    f.stacks.attach_at_front(2, {2, 2});
    CHECK(f.stacks.first(7) == 2);
    CHECK(f.unit(2).next.value == 4);
    CHECK(f.unit(4).previous.value == 2);
    CHECK(f.unit(4).next.value == 7);
    CHECK(tile == before);
    CHECK_FALSE(f.retained.attach_active);
}

TEST_CASE("U01 front attachment uses full arguments before signed short publication",
          "[unit][stack]") {
    Fixture f;
    f.live(0, {-200, -200});
    f.live(1);
    const auto tile = f.map.tile({2, 2});
    f.stacks.attach_at_front(0, {65538, -65534});
    CHECK(f.unit(0).x == 2);
    CHECK(f.unit(0).y == 2);
    CHECK(f.unit(0).next.value == -1);
    CHECK(f.unit(1).previous.value == -1);
    CHECK(f.map.tile({2, 2}) == tile);
}

TEST_CASE("U01 detached staging uses signed owner and empty attachment publishes W01 owner",
          "[unit][stack]") {
    for (unsigned owner : {1U, 8U, 9U, 128U, 255U}) {
        Fixture f;
        f.live(0, {2, 2}, owner);
        f.map.tile({4, 2}).fertility = 7;
        f.map.tile({4, 2}).features = 0x10;
        f.stacks.detach(0);
        const auto signed_owner =
            owner < 128 ? static_cast<int>(owner) : static_cast<int>(owner) - 256;
        CHECK(f.unit(0).x == -100 * (signed_owner + 1));
        CHECK(f.unit(0).y == f.unit(0).x);
        f.stacks.attach_at_front(0, {4, 2});
        CHECK(f.map.tile({4, 2}).fertility == 7);
        CHECK(f.map.tile({4, 2}).features == 0x11);
        CHECK(f.map.tile({4, 2}).owner ==
              (owner <= 8 ? std::optional{static_cast<std::uint8_t>(owner)} : std::nullopt));
    }
}

TEST_CASE("U01 self loop repair performs detach and front attachment", "[unit][stack]") {
    Fixture f;
    f.live(0);
    f.unit(0).previous.value = f.unit(0).next.value = 0;
    CHECK(f.stacks.raw_forward_count(0) == 2048);
    CHECK(f.stacks.validate(0) == 0);
    CHECK(f.reports == std::vector<StackDiagnostic>{{StackProblem::infinite, 0}});
    CHECK(f.unit(0).previous.value == -1);
    CHECK(f.unit(0).next.value == -1);
    CHECK(f.unit(0).x == 2);
    CHECK(f.unit(0).y == 2);
    CHECK(f.map.tile({2, 2}).features == 1);
    CHECK(f.map.tile({2, 2}).owner == 1);
    CHECK(f.retained.suppression == 0);
    CHECK_FALSE(f.retained.detach_active);
    CHECK_FALSE(f.retained.attach_active);
}

TEST_CASE("U01 finite 2048 member repair retains reversal before the negative terminal read",
          "[unit][stack]") {
    Fixture f;
    for (unsigned i = 0; i < 2048; ++i) {
        f.live(i);
        f.unit(i).previous.value = static_cast<std::int16_t>(static_cast<int>(i) - 1);
        f.unit(i).next.value = i == 2047 ? -7 : static_cast<std::int16_t>(i + 1);
    }
    SECTION("missing negative backing stops after placement") {
        CHECK_THROWS_AS(f.stacks.validate(0), std::out_of_range);
    }
    SECTION("supplied negative backing permits completion") {
        f.extra[-7].next.value = -9;
        CHECK(f.stacks.validate(0) == 0);
    }
    CHECK(f.reports == std::vector<StackDiagnostic>{{StackProblem::infinite, 0}});
    CHECK(f.extra_reads == std::vector<std::int32_t>{-7});
    CHECK(f.units.end() == 2048);
    CHECK_FALSE(f.retained.detach_active);
    CHECK_FALSE(f.retained.attach_active);
    for (unsigned i = 0; i < 2048; ++i) {
        CHECK(f.unit(i).previous.value == (i == 2047 ? -1 : static_cast<int>(i) + 1));
        // Detaching the old tail copies its -7 successor to the new tail (0).
        CHECK(f.unit(i).next.value == (i == 0 ? -7 : static_cast<int>(i) - 1));
        CHECK(f.unit(i).x == 2);
        CHECK(f.unit(i).y == 2);
    }
}

TEST_CASE("U01 recursive repair stop preserves placement markers and detached position",
          "[unit][stack]") {
    Fixture f;
    f.live(0);
    f.live(1);
    f.unit(0).previous.value = f.unit(0).next.value = 0;
    f.budget.validation_depth = 1;
    try {
        f.stacks.validate(0);
        FAIL("recursive validation should exhaust its declared depth");
    } catch (const StackResourceStop& stop) {
        CHECK(stop.reason() == StackStop::validation_depth);
    }
    CHECK(f.reports == std::vector<StackDiagnostic>{{StackProblem::infinite, 0}});
    CHECK(f.unit(0).x == -200);
    CHECK(f.unit(0).y == -200);
    CHECK(f.unit(0).previous.value == -1);
    CHECK(f.unit(0).next.value == -1);
    CHECK_FALSE(f.retained.detach_active);
    CHECK(f.retained.attach_active);
    CHECK(f.retained.suppression == 0);
    // Depth is an execution guard, not a stuck gameplay activity marker.
    f.budget.validation_depth = 64;
    CHECK(f.stacks.validate(1) == 1);
}

TEST_CASE("U01 backward cycles stop at explicit execution budget without invented repair",
          "[unit][stack]") {
    Fixture f;
    f.chain({0, 1});
    f.unit(0).previous.value = 1;
    const auto first = f.unit(0), second = f.unit(1);
    f.budget.steps = 40;
    try {
        f.stacks.validate(0);
        FAIL("backward cycle should exhaust its declared budget");
    } catch (const StackResourceStop& stop) {
        CHECK(stop.reason() == StackStop::steps);
    }
    CHECK(f.budget.steps == 0);
    CHECK(f.reports.empty());
    CHECK(f.unit(0) == first);
    CHECK(f.unit(1) == second);
}

TEST_CASE("U01 unbacked detach retains the earlier neighbor rewrite and activity marker",
          "[unit][stack]") {
    Fixture f;
    f.chain({1, 0});
    f.unit(0).next.value = 3000;
    CHECK_THROWS_AS(f.stacks.detach(0), std::out_of_range);
    CHECK(f.unit(1).next.value == 3000);
    CHECK(f.unit(0).previous.value == 1);
    CHECK(f.unit(0).next.value == 3000);
    CHECK(f.unit(0).x == 2);
    CHECK(f.retained.detach_active);
}
