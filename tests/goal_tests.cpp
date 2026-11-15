#include <catch2/catch_test_macros.hpp>

import std;
import openciv.tests.service_callbacks;
import openciv.formats.state_conversion;
import openciv.ai.goals;
import openciv.formats.save_records;

// A01 section 2 / Appendix A and W01 section 4. Independently
// authored specification cases; supplied U04 results are not original captures.

using namespace openciv;
using namespace openciv::ai;
namespace {
using Goal = state::CivilizationGoal;
struct Fixture {
    state::ProcessContext process;
    state::GameState world{process};
    GoalCallbacks services;
    explicit Fixture(bool allocate = true) {
        if (allocate)
            world.map.allocate(world::Geometry{20, 12});
        world.globals.turn_civ = 1;
        for (auto& civ : world.civilizations) {
            civ.goals.fill({-10, -20, -1, 0});
            civ.standing_goals.fill({-30, -40, -1, 0});
        }
        services.awake_with_moves = [](state::UnitSlot) { return true; };
        services.moves_left = [](state::UnitSlot) { return 4; };
    }
    SharedGoals operations() { return {world, process.rules->definitions, services}; }
    state::Civilization& civ() { return world.civilizations[1]; }
    state::Unit& unit(std::size_t index) { return world.entities.units.storage()[index]; }
    void live(std::size_t index, std::int32_t role = 2, world::Position position = {4, 4}) {
        auto& value = unit(index);
        value.token = state::UnitToken{static_cast<std::uint32_t>(index + 1)};
        value.owner = state::CivilizationSlot{1};
        value.type = state::UnitType{0};
        value.x = static_cast<std::int16_t>(position.x);
        value.y = static_cast<std::int16_t>(position.y);
        value.flags = 0x8912;
        value.ai_task = 0x37;
        value.destination_x = -12;
        value.destination_y = -14;
        process.rules->definitions.units[0].role = static_cast<state::RuleRole>(role);
        world.entities.units.set_end(std::max(world.entities.units.end(), index + 1));
        world.globals.unit_end = static_cast<std::int16_t>(world.entities.units.end());
    }
};
} // namespace

TEST_CASE("A01 duplicate admission compares retained role and priority but full coordinates",
          "[ai][goals]") {
    Fixture f;
    f.live(0);
    f.civ().goals[47] = {2, 4, 2, -128};
    f.civ().standing_goals[15] = {2, 4, 2, -128};
    const auto before = f.civ();
    auto goals = f.operations();
    goals.add_current(1, {2, 4}, 258, 128);
    goals.add_standing(1, {2, 4}, 258, 128);
    CHECK(f.civ() == before);

    // A free marker does not exclude a coordinate/role duplicate.
    f.civ().goals[0] = {2, 4, -1, 6};
    const auto free_before = f.civ();
    goals.add_current(1, {2, 4}, 255, 5);
    CHECK(f.civ() == free_before);
    goals.add_current(1, {65538, -65532}, 258, 128);
    CHECK(f.civ().goals[0] == Goal{2, 4, 2, -128});
    CHECK(f.civ().goals[47] == Goal{2, 4, 2, -128});
}

TEST_CASE("A01 higher priority duplicates are inserted and equal priorities stay ordered",
          "[ai][goals]") {
    Fixture f;
    f.civ().goals[0] = {2, 2, 1, 6};
    f.civ().goals[1] = {4, 4, 1, 4};
    f.civ().goals[2] = {6, 6, 1, 4};
    auto goals = f.operations();
    goals.add_current(1, {4, 4}, 1, 5);
    goals.add_current(1, {8, 8}, 1, 4);
    CHECK(f.civ().goals[0] == Goal{2, 2, 1, 6});
    CHECK(f.civ().goals[1] == Goal{4, 4, 1, 5});
    CHECK(f.civ().goals[2] == Goal{4, 4, 1, 4});
    CHECK(f.civ().goals[3] == Goal{6, 6, 1, 4});
    CHECK(f.civ().goals[4] == Goal{8, 8, 1, 4});
}

TEST_CASE("A01 current gaps stop at the first hole and standing gaps shift the whole tail",
          "[ai][goals]") {
    Fixture f;
    for (std::size_t i = 0; i < 16; ++i) {
        const Goal goal{static_cast<std::int16_t>(i), 3, 1, 2};
        f.civ().goals[i] = f.civ().standing_goals[i] = goal;
    }
    f.civ().goals[2] = f.civ().standing_goals[2] = {22, 23, -1, 99};
    f.civ().goals[47] = {90, 91, 6, 5};
    const auto before = f.civ();
    auto goals = f.operations();
    goals.add_current(1, {10, 10}, 7, 3);
    goals.add_standing(1, {10, 10}, 7, 3);
    CHECK(f.civ().goals[0] == Goal{10, 10, 7, 3});
    CHECK(f.civ().standing_goals[0] == Goal{10, 10, 7, 3});
    CHECK(f.civ().goals[1] == before.goals[0]);
    CHECK(f.civ().goals[2] == before.goals[1]);
    for (std::size_t i = 3; i < 48; ++i)
        CHECK(f.civ().goals[i] == before.goals[i]);
    for (std::size_t i = 1; i < 16; ++i)
        CHECK(f.civ().standing_goals[i] == before.standing_goals[i - 1]);
}

TEST_CASE("A01 insertion into a free current slot preserves the tail regardless of its priority",
          "[ai][goals]") {
    Fixture f;
    f.civ().goals[0] = {5, 6, -1, 127};
    f.civ().goals[1] = {7, 8, 4, 1};
    auto expected = f.civ();
    expected.goals[0] = {-32768, 32767, -2, -128};
    f.operations().add_current(1, {32768, -32769}, 254, 128);
    CHECK(f.civ() == expected);
    // Only -1 is free. Other negative roles still participate in admission.
    f.civ().goals.fill({5, 6, -2, 127});
    const auto occupied = f.civ();
    f.operations().add_current(1, {2, 2}, 1, 6);
    CHECK(f.civ() == occupied);
}

TEST_CASE("A01 full goal lists reject equal priorities and discard only the last on insertion",
          "[ai][goals]") {
    Fixture f;
    f.civ().goals.fill({2, 2, 0, 3});
    f.civ().standing_goals.fill({4, 4, 0, 3});
    f.civ().goals[47] = {8, 8, 0, 3};
    f.civ().standing_goals[15] = {9, 9, 0, 3};
    auto goals = f.operations();
    const auto before = f.civ();
    goals.add_current(1, {6, 6}, 1, 3);
    goals.add_standing(1, {6, 6}, 1, 3);
    CHECK(f.civ() == before);
    goals.add_current(1, {6, 6}, 1, 4);
    goals.add_standing(1, {6, 6}, 1, 4);
    CHECK(f.civ().goals[0] == Goal{6, 6, 1, 4});
    CHECK(f.civ().standing_goals[0] == Goal{6, 6, 1, 4});
    for (std::size_t i = 1; i < 48; ++i)
        CHECK(f.civ().goals[i] == before.goals[i - 1]);
    for (std::size_t i = 1; i < 16; ++i)
        CHECK(f.civ().standing_goals[i] == before.standing_goals[i - 1]);
}

TEST_CASE("A01 negation preserves coordinates roles zero and signed byte minimum", "[ai][goals]") {
    for (int priority = -128; priority <= 127; ++priority) {
        CAPTURE(priority);
        Goal value{123, -456, -19, static_cast<std::int8_t>(priority)};
        negate_goal_priority(value);
        CHECK(value ==
              Goal{123, -456, -19, static_cast<std::int8_t>(priority > 0 ? -priority : priority)});
    }
}

TEST_CASE("A01 refresh ages current goals then reissues standing goals in storage order",
          "[ai][goals]") {
    Fixture f;
    f.civ().goals[0] = {0, 0, 0, 6};
    f.civ().goals[1] = {2, 2, 0, -5};
    f.civ().goals[2] = {4, 4, 1, 0};
    f.civ().goals[3] = {6, 6, 0, -128};
    f.civ().goals[4] = {8, 8, -1, 7};
    f.civ().standing_goals[0] = {12, 2, 0, 3};
    f.civ().standing_goals[1] = {14, 2, -2, 127};
    f.civ().standing_goals[2] = {16, 2, 1, 3};
    f.civ().standing_goals[3] = {18, 2, 1, 0};
    const auto standing = f.civ().standing_goals;
    f.operations().refresh(1);
    CHECK(f.civ().goals[0] == Goal{12, 2, 0, 3});
    CHECK(f.civ().goals[1] == Goal{16, 2, 1, 3});
    CHECK(f.civ().goals[2] == Goal{18, 2, 1, 0});
    CHECK(f.civ().goals[3] == Goal{0, 0, 0, -6});
    CHECK(f.civ().goals[4] == Goal{4, 4, 1, 0});
    CHECK(f.civ().goals[5] == Goal{-10, -20, -1, 0});
    CHECK(f.civ().standing_goals == standing);
}

TEST_CASE("A01 within-game refresh before standing reset retains coordinates and expires next time",
          "[ai][goals]") {
    Fixture f;
    f.civ().goals[0] = {2, 2, 1, 5};
    f.civ().goals[1] = {4, 4, 1, -128};
    f.civ().goals[2] = {6, 6, 1, 0};
    f.civ().standing_goals[0] = {8, 8, 1, 7};
    const auto standing = f.civ().standing_goals;
    f.world.civilizations[2] = f.civ();
    const auto other = f.world.civilizations[2];
    auto goals = f.operations();
    goals.refresh(1);
    reset_standing_goals(f.civ());
    CHECK(f.civ().goals[0] == Goal{8, 8, 1, 7});
    CHECK(f.civ().goals[1] == Goal{2, 2, 1, -5});
    CHECK(f.civ().goals[2] == Goal{6, 6, 1, 0});
    for (std::size_t i = 0; i < 16; ++i)
        CHECK(f.civ().standing_goals[i] == Goal{standing[i].x, standing[i].y, -1, 0});
    goals.refresh(1);
    CHECK(f.civ().goals[0] == Goal{8, 8, 1, -7});
    CHECK(f.civ().goals[1] == Goal{2, 2, -1, -5});
    CHECK(f.civ().goals[2] == Goal{6, 6, 1, 0});
    goals.refresh(1);
    CHECK(f.civ().goals[0] == Goal{8, 8, -1, -7});
    CHECK(f.civ().goals[2] == Goal{6, 6, 1, 0});
    CHECK(f.world.civilizations[2] == other);
}

TEST_CASE("A01 refresh preserves expired priorities even in free records", "[ai][goals]") {
    Fixture f;
    f.civ().goals[0] = {2, 2, 1, -128};
    f.civ().goals[1] = {4, 4, -1, 127};
    f.civ().goals[2] = {6, 6, -2, 0};
    auto expected = f.civ();
    expected.goals[0].role = -1;
    expected.goals[1].priority = -127;
    f.operations().refresh(1);
    CHECK(f.civ() == expected);
}

TEST_CASE("A01 priority query uses signed running result instead of an ordinary maximum",
          "[ai][goals]") {
    Fixture f;
    f.civ().goals[0] = {2, 4, 1, -128};
    f.civ().goals[1] = {2, 4, 1, -3};
    CHECK(goal_priority_at(f.civ(), {2, 4}, 1) == 3);
    std::swap(f.civ().goals[0], f.civ().goals[1]);
    CHECK(goal_priority_at(f.civ(), {2, 4}, 1) == 128);
    f.civ().goals[47] = {2, 4, 1, 0};
    CHECK(goal_priority_at(f.civ(), {2, 4}, 1) == 0);
    CHECK(goal_priority_at(f.civ(), {65538, 4}, 1) == 0);
    f.civ().goals[0] = {2, 4, -1, -7};
    CHECK(goal_priority_at(f.civ(), {2, 4}, -1) == 7);
    CHECK(goal_priority_at(f.civ(), {2, 4}, 4) == 0);
}

TEST_CASE("A01 standing clear uses live topology inclusive weighted distance and retained role",
          "[ai][goals]") {
    Fixture f;
    f.civ().standing_goals[0] = {18, 4, 18, -128};
    f.civ().standing_goals[1] = {4, 6, 18, 0};
    f.civ().standing_goals[2] = {4, 8, 18, 6};
    f.civ().standing_goals[3] = {0, 4, 1, 7};
    const auto before = f.civ();
    auto goals = f.operations();
    goals.clear_standing_near(1, 274, {0, 4}, 2);
    CHECK(f.civ().standing_goals[0] == Goal{18, 4, -1, 0});
    CHECK(f.civ().standing_goals[1] == before.standing_goals[1]); // weighted=3
    CHECK(f.civ().standing_goals[2] == before.standing_goals[2]);
    CHECK(f.civ().standing_goals[3] == before.standing_goals[3]);
    f.civ() = before;
    f.world.globals.rules |= 0x8000;
    goals.clear_standing_near(1, 18, {0, 4}, 3);
    CHECK(f.civ().standing_goals[0] == before.standing_goals[0]);
    CHECK(f.civ().standing_goals[1] == Goal{4, 6, -1, 0});
    CHECK(f.civ().standing_goals[2] == before.standing_goals[2]);
    goals.clear_standing_near(1, 1, {0, 4}, -1);
    CHECK(f.civ().standing_goals[3] == before.standing_goals[3]);
    goals.clear_standing_near(1, 1, {0, 4}, 0);
    CHECK(f.civ().standing_goals[3] == Goal{0, 4, -1, 0});
    CHECK(f.civ().goals == before.goals);
}

TEST_CASE("A01 redirection requires current AI civilization and full naval or air role",
          "[ai][goals]") {
    Fixture f;
    f.live(0);
    SECTION("human") {
        f.world.globals.human_players = 2;
    }
    SECTION("other turn") {
        f.world.globals.turn_civ = 2;
    }
    SECTION("other role") {
        f.operations().add_current(1, {2, 4}, 1, 5);
    }
    SECTION("wide role") {
        f.operations().add_current(1, {2, 4}, 258, 5);
    }
    if (f.civ().goals[0].role == -1)
        f.operations().add_current(1, {2, 4}, 2, 5);

    CHECK(f.unit(0).orders == state::UnitOrder::none);
    CHECK(f.civ().goals[0].priority == 5);
}

TEST_CASE("A01 redirection filters awake status role ownership region and distance",
          "[ai][goals]") {
    Fixture f;
    for (std::size_t i = 0; i < 8; ++i)
        f.live(i);
    f.unit(0).token = state::UnitToken{0};
    f.unit(1).owner = state::CivilizationSlot{2};
    f.unit(2).type = state::UnitType{255}; // No read when awake is false.
    f.unit(3).type = state::UnitType{1};
    f.process.rules->definitions.units[1].role = static_cast<state::RuleRole>(254);
    f.unit(4).x = 6;
    f.world.map.tile({6, 4}).region = 1;
    f.unit(5).x = 8; // weighted=4, so four moves do not suffice.
    f.unit(6).x = 5;
    f.unit(6).y = 5; // weighted=2, inclusive gate succeeds.
    f.unit(7).x = 4;
    auto expected6 = f.unit(6), expected7 = f.unit(7);
    expected6.orders = expected7.orders = state::UnitOrder::go_to;
    expected6.destination_x = expected7.destination_x = 2;
    expected6.destination_y = expected7.destination_y = 4;
    f.services.awake_with_moves = [](state::UnitSlot slot) { return slot != state::UnitSlot{2}; };
    f.operations().add_current(1, {2, 4}, 2, 5);

    for (std::size_t i = 0; i < 6; ++i)
        CHECK(f.unit(i).orders == state::UnitOrder::none);
    CHECK(f.unit(6) == expected6);
    CHECK(f.unit(7) == expected7);
}

TEST_CASE("A01 redirection precedes capacity rejection but follows duplicate suppression",
          "[ai][goals]") {
    Fixture f;
    f.live(0, 3);
    f.civ().goals.fill({0, 0, 0, 127});
    const auto before = f.civ();
    f.operations().add_current(1, {2, 4}, 3, 3);
    CHECK(f.civ() == before);
    CHECK(f.unit(0).orders == state::UnitOrder::go_to);

    f.civ().goals[47] = {2, 4, 3, 3};
    f.unit(0).orders = state::UnitOrder::none;
    f.operations().add_current(1, {2, 4}, 3, 3);

    CHECK(f.unit(0).orders == state::UnitOrder::none);
}

TEST_CASE("A01 redirection stops on missing services or rule backing after prior effects",
          "[ai][goals]") {
    Fixture f;
    f.live(0);
    f.live(1);
    const auto before = f.civ();
    SECTION("missing awake") {
        f.services.awake_with_moves = {};
        CHECK_THROWS_AS(f.operations().add_current(1, {2, 4}, 2, 3), std::bad_function_call);
        CHECK(f.unit(0).orders == state::UnitOrder::none);
    }
    SECTION("missing moves") {
        f.services.moves_left = {};
        CHECK_THROWS_AS(f.operations().add_current(1, {2, 4}, 2, 3), std::bad_function_call);
    }
    SECTION("later type unbacked") {
        f.unit(1).type = state::UnitType{255};
        CHECK_THROWS_AS(f.operations().add_current(1, {2, 4}, 2, 3), std::out_of_range);
        CHECK(f.unit(0).orders == state::UnitOrder::go_to);
        CHECK(f.unit(1).orders == state::UnitOrder::none);
    }
    CHECK(f.civ() == before);
}

TEST_CASE("A01 backing checks occur only at consumed inputs and invalid actors cannot write",
          "[ai][goals]") {
    Fixture f(false);
    f.services = {};
    auto goals = f.operations();
    CHECK_NOTHROW(goals.add_current(1, {2, 4}, 2, 3));          // No live units.
    CHECK_NOTHROW(goals.clear_standing_near(1, 18, {2, 4}, 3)); // No matching role.
    goals.add_standing(1, {2, 4}, 18, 2);
    const auto before = f.civ();
    CHECK_THROWS_AS(goals.clear_standing_near(1, 18, {2, 4}, 3), std::logic_error);
    CHECK_THROWS_AS(goals.add_current(-1, {2, 4}, 0, 3), std::out_of_range);
    CHECK_THROWS_AS(goals.add_standing(8, {2, 4}, 0, 3), std::out_of_range);
    CHECK_THROWS_AS(goals.refresh(8), std::out_of_range);
    CHECK_THROWS_AS(goals.clear_standing_near(-1, 18, {2, 4}, 3), std::out_of_range);
    CHECK(f.civ() == before);
}

TEST_CASE("A01 distance-only clearing needs retained geometry but no tile allocation",
          "[ai][goals]") {
    Fixture f;
    f.civ().standing_goals[0] = {18, 4, 18, -128};
    f.world.map.release();
    f.operations().clear_standing_near(1, 18, {0, 4}, 1);
    CHECK_FALSE(f.world.map.allocated());
    CHECK(f.civ().standing_goals[0] == Goal{18, 4, -1, 0});
}

TEST_CASE("A01 full coordinate distance comparison retains wrapping multiplication",
          "[ai][goals]") {
    Fixture f;
    f.live(0, 2, {0, 0});
    f.world.globals.rules = 0x8000;
    f.services.moves_left = [](state::UnitSlot) { return 0; };
    // W01 weighted distance 1610612735 doubles to signed -1073741826.
    // The out-of-map target uses the process fallback's region, also zero.
    f.operations().add_current(1, {2147483647, 0}, 2, 4);
    CHECK(f.unit(0).orders == state::UnitOrder::go_to);
    CHECK(f.unit(0).destination_x == -1);
    CHECK(f.civ().goals[0] == Goal{-1, 0, 2, 4});
}

TEST_CASE("A01 shared goal writes retain unrelated civilization bytes and round trip through F06",
          "[ai][goals]") {
    Fixture f;
    auto& civ = f.civ();
    civ = formats::to_live(
        formats::decode_civilization(formats::Bytes(formats::civilization_record_size, 0xa5)));
    const auto before = formats::encode_civilization(civ);
    reset_standing_goals(civ);
    f.operations().add_standing(1, {32768, -32769}, 511, 383);
    const auto encoded = formats::encode_civilization(civ);
    for (std::size_t i = 0; i < 0x534; ++i)
        CHECK(encoded[i] == before[i]);
    CHECK(civ.standing_goals[0] == Goal{-32768, 32767, -1, 127});
    CHECK(formats::to_live(formats::decode_civilization(encoded)) == civ);
}
