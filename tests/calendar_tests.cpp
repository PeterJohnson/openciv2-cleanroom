// F04 section 5, F05 section 4, gameplay-format section 2. Expectations are
// authored from the contracts; these are not original-program captures.
#include "turn/calendar.hpp"
#include "session/setup_calendar.hpp"
#include "formats/content_pack.hpp"
#include "base/hash.hpp"
#include "builtin_gameplay_data.hpp"
#include <catch2/catch_test_macros.hpp>
#include <limits>
#include <map>

using namespace openciv;
namespace {
constexpr auto minimum = std::numeric_limits<std::int32_t>::min();
constexpr auto maximum = std::numeric_limits<std::int32_t>::max();
const state::CalendarValues& classic() {
    return formats::content::builtin_gameplay()->values.calendar;
}
state::GlobalValues ordinary(std::uint8_t difficulty = 2, std::uint16_t flags = 0) {
    state::GlobalValues globals;
    globals.difficulty = difficulty;
    globals.game_flags = flags;
    globals.first_launch_turn = -1;
    return globals;
}
std::shared_ptr<const formats::content::GameplayBundle>
custom(const std::array<state::CalendarPeriod, 6>& periods) {
    using namespace formats::content;
    std::map<std::string, formats::Bytes> files;
    for (const auto& [path, data] : builtin_gameplay_files)
        files.emplace(path, formats::Bytes(data.begin(), data.end()));
    auto manifest = parse_json(builtin_gameplay_files[0].second, "calendar fixture").value;
    const auto& bytes = files.at("calendar.json");
    auto document =
        parse_json({reinterpret_cast<const char*>(bytes.data()), bytes.size()}, "calendar fixture")
            .value;
    for (const auto name : state::calendar_profiles)
        for (std::size_t i = 0; i < periods.size(); ++i) {
            auto& row = document["profiles"][std::string(name)]["periods"][i];
            row["startYear"] = jt::Json(periods[i].start_year);
            row["turnCount"] = jt::Json(periods[i].turn_count);
            row["yearStep"] = jt::Json(periods[i].year_step);
        }
    const auto encoded = document.toString();
    files["calendar.json"] = {encoded.begin(), encoded.end()};
    manifest["components"]["calendar"]["sha256"] = jt::Json(base::sha256(files["calendar.json"]));
    manifest["mode"] = jt::Json("custom");
    manifest["id"] = jt::Json("authored.calendar");
    manifest["revision"] = jt::Json(2);
    const auto header = manifest.toString();
    return load_gameplay(formats::Bytes(header.begin(), header.end()), [&](auto path) {
        return Source{std::string(path), files.at(std::string(path))};
    });
}
} // namespace

TEST_CASE("F04 calendar selects profiles from difficulty and both retained map classifications",
          "[calendar][gameplay]") {
    // At turn 275, profiles 0..3 yield 1240, 1620, 1798, 1874.
    constexpr std::array<std::array<std::int32_t, 4>, 6> expected{{{1240, 1240, 1240, 1240},
                                                                   {1240, 1240, 1620, 1240},
                                                                   {1620, 1240, 1798, 1620},
                                                                   {1798, 1620, 1874, 1798},
                                                                   {1874, 1798, 1874, 1874},
                                                                   {1874, 1874, 1874, 1874}}};
    for (std::uint8_t difficulty = 0; difficulty < 6; ++difficulty)
        for (unsigned classification = 0; classification < 4; ++classification) {
            CAPTURE(difficulty, classification);
            const auto globals =
                ordinary(difficulty, static_cast<std::uint16_t>(classification * 4));
            CHECK(turn::calendar_year(275, globals, {}, classic()) ==
                  expected[difficulty][classification]);
        }
    CHECK(turn::calendar_year(275, ordinary(255, 0xfff3), {}, classic()) == 1874);
}

TEST_CASE("F04 ordinary calendar retains duplicate opening turns and every period transition",
          "[calendar][gameplay]") {
    struct Point {
        std::uint8_t difficulty;
        std::int32_t turn, year;
    };
    const Point points[]{
        {0, 0, -4000},  {0, 1, -4000},  {0, 2, -3980},  {0, 200, -20},  {0, 201, 1},
        {0, 202, 20},   {0, 251, 1000}, {0, 252, 1010}, {0, 301, 1500}, {0, 302, 1505},
        {0, 351, 1750}, {0, 352, 1752}, {0, 401, 1850}, {0, 402, 1851}, {0, 451, 1900},
        {0, 452, 1901}, {0, 551, 2000}, {0, 552, 2001}, {2, 0, -4000},  {2, 1, -4000},
        {2, 2, -3950},  {2, 61, -1000}, {2, 62, -975},  {2, 100, -25},  {2, 101, 1},
        {2, 102, 10},   {2, 251, 1500}, {2, 252, 1505}, {2, 301, 1750}, {2, 302, 1752},
        {2, 351, 1850}, {2, 352, 1851}, {2, 401, 1900}, {2, 402, 1901}, {3, 61, -1000},
        {3, 62, -975},  {3, 101, 1},    {3, 102, 20},   {3, 151, 1000}, {3, 152, 1010},
        {3, 201, 1500}, {3, 202, 1505}, {3, 251, 1750}, {3, 252, 1752}, {3, 301, 1850},
        {3, 302, 1851}, {4, 61, -1000}, {4, 62, -975},  {4, 101, 1},    {4, 102, 20},
        {4, 176, 1500}, {4, 177, 1510}, {4, 201, 1750}, {4, 202, 1752}, {4, 251, 1850},
        {4, 252, 1851}, {4, 301, 1900}, {4, 302, 1901}};
    for (const auto point : points) {
        CAPTURE(point.difficulty, point.turn);
        CHECK(turn::calendar_year(point.turn, ordinary(point.difficulty), {}, classic()) ==
              point.year);
    }
}

TEST_CASE("F04 signed turns preserve wide results and explicit modular intermediate arithmetic",
          "[calendar][gameplay]") {
    const auto globals = ordinary(0);
    CHECK(turn::calendar_year(-1, globals, {}, classic()) == -4040);
    CHECK(turn::calendar_year(-32768, globals, {}, classic()) == -659380);
    CHECK(turn::calendar_year(32767, globals, {}, classic()) == 34216);
    CHECK(turn::calendar_year(maximum, globals, {}, classic()) == -2147482200);
    CHECK(turn::calendar_year(minimum, globals, {}, classic()) == -2147482199);
    CHECK(turn::calendar_year(-32768, ordinary(5), {}, classic()) == -1642450);
}

TEST_CASE("F04 first launch adds its progression supplement after ordinary zero substitution",
          "[calendar][gameplay]") {
    auto globals = ordinary(0);
    globals.first_launch_turn = 21;
    CHECK(turn::calendar_year(0, globals, {}, classic()) == -4000);
    CHECK(turn::calendar_year(20, globals, {}, classic()) == -3620);
    CHECK(turn::calendar_year(21, globals, {}, classic()) == -3600);
    CHECK(turn::calendar_year(41, globals, {}, classic()) == -3580);
    globals.first_launch_turn = 201;
    CHECK(turn::calendar_year(203, globals, {}, classic()) == 3);
    globals = ordinary(4);
    globals.first_launch_turn = 100;
    CHECK(turn::calendar_year(125, globals, {}, classic()) ==
          0); // No post-extra zero substitution.
    globals.first_launch_turn = 0;
    CHECK(turn::calendar_year(1, globals, {}, classic()) == -3999);
    CHECK(turn::calendar_year(41, globals, {}, classic()) == -3959);
}

TEST_CASE("F04 launch arithmetic wraps before its signed nonnegative test",
          "[calendar][gameplay]") {
    auto globals = ordinary(0);
    globals.first_launch_turn = 10;
    CHECK(turn::calendar_year(minimum, globals, {}, classic()) == 2147479818);
    globals.first_launch_turn = 0;
    CHECK(turn::calendar_year(minimum, globals, {}, classic()) == -2147482199);
    globals.first_launch_turn = -32768;
    CHECK(turn::calendar_year(41, globals, {}, classic()) == -3200);
    globals.first_launch_turn = 32767;
    CHECK(turn::calendar_year(32768, globals, {}, classic()) == 34217);
}

TEST_CASE("F04 scenario calendar requires the active flag and both nonzero parameters",
          "[calendar][gameplay]") {
    auto globals = ordinary(2, 0x80);
    state::ScenarioParameters scenario;
    scenario.year_increment = 5;
    scenario.start_year = 100;
    CHECK(turn::calendar_year(0, globals, scenario, classic()) == 99);
    CHECK(turn::calendar_year(1, globals, scenario, classic()) == 104);
    CHECK(turn::calendar_year(21, globals, scenario, classic()) == 204);
    CHECK(turn::calendar_year(-2, globals, scenario, classic()) == 89);
    scenario.year_increment = -5;
    CHECK(turn::calendar_year(21, globals, scenario, classic()) == 204);
    globals.game_flags = 0;
    CHECK(turn::calendar_year(21, globals, scenario, classic()) == -3000);
    globals.game_flags = 0x80;
    scenario.start_year = 0;
    CHECK(turn::calendar_year(21, globals, scenario, classic()) == -3000);
    scenario.start_year = 100;
    scenario.year_increment = 0;
    CHECK(turn::calendar_year(21, globals, scenario, classic()) == -3000);
}

TEST_CASE("F04 scenario zero year is retained and first launch still applies afterward",
          "[calendar][gameplay]") {
    auto globals = ordinary(5, 0x8c);
    state::ScenarioParameters scenario;
    scenario.start_year = 1;
    scenario.year_increment = -1;
    CHECK(turn::calendar_year(0, globals, scenario, classic()) == 0);
    globals.first_launch_turn = 0;
    CHECK(turn::calendar_year(41, globals, scenario, classic()) == 41);
    scenario.start_year = -100;
    scenario.year_increment = -12;
    globals.first_launch_turn = 21;
    CHECK(turn::calendar_year(41, globals, scenario, classic()) == 171);
}

TEST_CASE("F04 scenario multiplication and addition use signed 32-bit wrapping",
          "[calendar][gameplay]") {
    const auto globals = ordinary(0, 0x80);
    state::ScenarioParameters scenario;
    scenario.start_year = 1;
    scenario.year_increment = -32768;
    CHECK(turn::calendar_year(65536, globals, scenario, classic()) == minimum);
    CHECK(turn::calendar_year(131072, globals, scenario, classic()) == 0);
    scenario.start_year = -32768;
    scenario.year_increment = 1;
    CHECK(turn::calendar_year(minimum, globals, scenario, classic()) == 2147450879);
}

TEST_CASE("F04 calendar reads actual decoded custom JSON without a classic operand fallback",
          "[calendar][gameplay][content]") {
    const auto bundle =
        custom({{{-12, 2, 3}, {-6, 2, 3}, {0, 2, 5}, {10, 2, 7}, {24, 2, 11}, {46, 2, 13}}});
    CHECK(bundle->custom_mode);
    CHECK_FALSE(bundle->classic_values);
    const std::array<std::int32_t, 16> years{-12, -12, -9, -6, -3, 1,  5,  10,
                                             17,  24,  35, 46, 59, 72, 73, 74};
    for (std::size_t i = 0; i < years.size(); ++i)
        CHECK(turn::calendar_year(static_cast<std::int32_t>(i), ordinary(2), {},
                                  bundle->values.calendar) == years[i]);
    CHECK(turn::calendar_year(14, ordinary(2), {}, classic()) == -3350);
    auto globals = ordinary(2);
    globals.first_launch_turn = 5;
    CHECK(turn::calendar_year(7, globals, {}, bundle->values.calendar) == 3);
    CHECK(turn::calendar_year(0, ordinary(0), {}, bundle->values.calendar) == -12);
    CHECK(turn::calendar_year(0, ordinary(5), {}, bundle->values.calendar) == -12);
}

TEST_CASE("F04 admitted custom calendars keep wrapping outside their positive period endpoints",
          "[calendar][gameplay][content]") {
    const auto bundle =
        custom({{{minimum, 1, maximum}, {-1, 1, 1}, {0, 1, 1}, {1, 1, 1}, {2, 1, 1}, {3, 1, 1}}});
    CHECK(turn::calendar_year(-2, ordinary(), {}, bundle->values.calendar) == 3);
    CHECK(turn::calendar_year(-1, ordinary(), {}, bundle->values.calendar) == -2147483646);
    CHECK(turn::calendar_year(1, ordinary(), {}, bundle->values.calendar) == minimum);
    CHECK(turn::calendar_year(2, ordinary(), {}, bundle->values.calendar) == -1);
    CHECK(turn::calendar_year(3, ordinary(), {}, bundle->values.calendar) == 1);
    CHECK(turn::calendar_year(maximum, ordinary(), {}, bundle->values.calendar) == 2147483644);
}

TEST_CASE("F04 calendar is an observation without writes to globals scenario or selected values",
          "[calendar][gameplay]") {
    auto globals = ordinary(3, 0xfffc);
    globals.turn = 123;
    globals.year = 456;
    globals.first_launch_turn = 21;
    state::ScenarioParameters scenario;
    scenario.flags = 0xffff;
    scenario.start_year = -400;
    scenario.year_increment = -12;
    scenario.maximum_turns = 90;
    const auto before_globals = globals;
    const auto before_scenario = scenario;
    const auto before_calendar = classic();
    CHECK(turn::calendar_year(41, globals, scenario, classic()) == -129);
    CHECK(globals == before_globals);
    CHECK(scenario == before_scenario);
    CHECK(classic() == before_calendar);
}

TEST_CASE("F05 acceleration years use F04 after clearing the launch marker without resetting state",
          "[calendar][setup-calendar][session]") {
    state::ProcessContext process;
    session::SessionRuntime runtime{process, base::RuntimeId{95}};
    auto& world = runtime.world();
    world.globals = ordinary(2);
    world.globals.turn = 77;
    world.globals.year = 1200;
    world.globals.first_launch_turn = 0;
    world.scenario.start_year = 123;
    world.scenario.year_increment = 7;
    const auto scenario = world.scenario;
    const auto preferences = process.ordinary_preferences;
    auto expected = world.globals;
    expected.first_launch_turn = -1;
    CHECK(session::prepare_acceleration_years(runtime, classic()) ==
          std::array<std::uint32_t, 3>{4000, 3000, 2000});
    CHECK(world.globals == expected);
    CHECK(world.scenario == scenario);
    CHECK(process.ordinary_preferences == preferences);
    CHECK_FALSE(runtime.active());
    world.globals.game_flags = 4;
    CHECK(session::prepare_acceleration_years(runtime, classic()) ==
          std::array<std::uint32_t, 3>{4000, 3600, 3200});
    world.globals.game_flags = 8;
    CHECK(session::prepare_acceleration_years(runtime, classic()) ==
          std::array<std::uint32_t, 3>{4000, 3000, 2000});
}

TEST_CASE("F05 acceleration labels preserve incoming scenario calendar and return magnitudes",
          "[calendar][setup-calendar][session]") {
    state::ProcessContext process;
    session::SessionRuntime runtime{process, base::RuntimeId{96}};
    runtime.world().globals = ordinary(0, 0x80);
    runtime.world().scenario.start_year = -100;
    runtime.world().scenario.year_increment = -12;
    CHECK(session::prepare_acceleration_years(runtime, classic()) ==
          std::array<std::uint32_t, 3>{101, 151, 391});
    runtime.world().scenario.start_year = 1;
    runtime.world().scenario.year_increment = 1;
    CHECK(session::prepare_acceleration_years(runtime, classic()) ==
          std::array<std::uint32_t, 3>{0, 21, 41});
    const auto bundle = custom({{{minimum, 1, 1},
                                 {minimum + 1, 1, 1},
                                 {minimum + 2, 1, 1},
                                 {minimum + 3, 1, 1},
                                 {minimum + 4, 1, 1},
                                 {minimum + 5, 1, 1}}});
    runtime.world().globals.game_flags = 0;
    CHECK(session::prepare_acceleration_years(runtime, bundle->values.calendar) ==
          std::array<std::uint32_t, 3>{2147483648U, 2147483628U, 2147483608U});
}

TEST_CASE("F05 acceleration labels publish existing state without advancing random contexts",
          "[calendar][setup-calendar][session]") {
    state::ProcessContext process;
    session::SessionRuntime runtime{process, base::RuntimeId{97}};
    runtime.world().globals = ordinary(5);
    runtime.begin_client_session({17, 29});
    auto& unit = runtime.world().entities.units.storage()[0];
    unit.token = state::UnitToken{1};
    unit.owner = state::CivilizationSlot{1};
    runtime.world().entities.units.set_end(1);
    const auto client = runtime.bind(runtime.add_participant(state::CivilizationSlot{1}),
                                     session::ControllerKind::frontend);
    unit.x = 123;
    const auto gameplay = runtime.gameplay_random().checkpoint();
    const auto presentation = runtime.presentation_random().checkpoint();
    CHECK(session::prepare_acceleration_years(runtime, classic()) ==
          std::array<std::uint32_t, 3>{4000, 3000, 2000});
    REQUIRE(client.own_units().records.size() == 1);
    CHECK(client.own_units().records[0].x == 123);
    CHECK(runtime.gameplay_random().checkpoint() == gameplay);
    CHECK(runtime.presentation_random().checkpoint() == presentation);
}
