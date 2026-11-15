#include "service_callbacks.hpp"
// F03 section 8/Appendix A, F06 option encodings and P10 owning audio boundary.
// Independently authored specification expectations; audio children are supplied.
#include "configuration_fixture.hpp"
#include "rules/double_movement.hpp"

using namespace openciv;
using namespace openciv::session;
namespace {
struct Fixture : test::ConfigurationFixture {
    GameOptionCallbacks game;
    GraphicsOptionCallbacks graphics;
    Fixture() {
        game.preferences = graphics.preferences = preferences;
        game.stop_music = [&] { trace.push_back("stop"); };
        game.reinitialize_music = [&] { trace.push_back("reinitialize"); };
        game.synchronize_music = [&] { trace.push_back("synchronize"); };
        graphics.heralds_available = [&] {
            trace.push_back("available");
            return true;
        };
        graphics.low_memory_notice = [&] { trace.push_back("low-memory"); };
    }
};
} // namespace

TEST_CASE("F03 cancelled option groups do not touch world preferences randomness or services",
          "[configuration][session]") {
    Fixture f;
    f.runtime.world().globals.game_options = 0xffffffffU;
    f.runtime.world().globals.message_options = 0xffff;
    const auto globals = f.runtime.world().globals;
    const auto preferences = f.process.ordinary_preferences;
    const auto random = f.runtime.gameplay_random().checkpoint();
    CHECK_FALSE(configure_game_options(f.runtime, {}, {}));
    CHECK_FALSE(configure_graphics_options(f.runtime, {}, {}));
    CHECK_FALSE(configure_message_options(f.runtime, {}, f.preferences));
    CHECK(f.runtime.world().globals == globals);
    CHECK(f.process.ordinary_preferences == preferences);
    CHECK(f.runtime.gameplay_random().checkpoint() == random);
    CHECK(f.trace.empty());
}

TEST_CASE("F03 game checkboxes replace exactly their eleven bits and persist the complete word",
          "[configuration][session]") {
    constexpr std::array<std::uint32_t, 11> expected_bits{
        0x10, 0x8, 0x4000, 0x2000, 0x1000, 0x800, 0x400, 0x200, 0x100, 0x80, 0x40};
    for (std::size_t selected = 0; selected < expected_bits.size(); ++selected) {
        Fixture f;
        GameOptionChoices choices{};
        choices[selected] = true;
        f.runtime.world().globals.game_options = 0xffffffffU;
        REQUIRE(configure_game_options(f.runtime, choices, f.game));
        const auto expected = 0xffff8027U | expected_bits[selected];
        CHECK(f.runtime.world().globals.game_options == expected);
        CHECK(f.process.ordinary_preferences.game_options == expected);
        REQUIRE(f.written.size() == 2);
        CHECK(formats::decode_ordinary_preferences(f.written[0]).game_options == expected);
        CHECK(formats::decode_ordinary_preferences(f.written[0]).disabled_messages == 0x9876);
    }
}

TEST_CASE("F03 music transition paths precede the remaining game checkboxes and preference save",
          "[configuration][session]") {
    for (const bool old_music : {false, true})
        for (const bool music : {false, true}) {
            Fixture f;
            f.runtime.world().globals.game_options = old_music ? 8U : 0U;
            GameOptionChoices choices{};
            choices.fill(true);
            choices[1] = music;
            const auto audio = [&](std::string name) {
                CHECK((f.runtime.world().globals.game_options & 0x7fc0U) == 0);
                CHECK(f.runtime.world().globals.game_options == (music ? 0x18U : 0x10U));
                CHECK(f.process.ordinary_preferences.game_options == 0xdeadbeefU);
                f.trace.push_back(std::move(name));
            };
            f.game.stop_music = [&] { audio("stop"); };
            f.game.reinitialize_music = [&] { audio("reinitialize"); };
            f.game.synchronize_music = [&] { audio("synchronize"); };
            CHECK(configure_game_options(f.runtime, choices, f.game));
            std::vector<std::string> expected;
            if (!music)
                expected.push_back("stop");
            else {
                if (!old_music)
                    expected.push_back("reinitialize");
                expected.push_back("synchronize");
            }
            expected.insert(expected.end(), {"open", "write:72", "write:444", "close"});
            CHECK(f.trace == expected);
            CHECK(f.runtime.world().globals.game_options == (music ? 0x7fd8U : 0x7fd0U));
        }
}

TEST_CASE("F03 audio callbacks publish live state before later children and persistence",
          "[configuration][session]") {
    Fixture f;
    auto& unit = f.runtime.world().entities.units.storage()[0];
    unit.token = state::UnitToken{1};
    unit.owner = state::CivilizationSlot{1};
    f.runtime.world().entities.units.set_end(1);
    const auto client = f.runtime.bind(f.runtime.add_participant(state::CivilizationSlot{1}),
                                       ControllerKind::frontend);
    unit.x = 7;
    f.game.reinitialize_music = [&] {
        REQUIRE(client.own_units().records.size() == 1);
        CHECK(client.own_units().records[0].x == 7);
        unit.x = 8;
    };
    f.game.synchronize_music = [&] {
        CHECK(client.own_units().records[0].x == 8);
        f.runtime.world().globals.game_options |= 0x80008020U;
    };
    f.after_open = [&] {
        CHECK(f.process.ordinary_preferences.game_options == 0x8000c028U);
        unit.x = 9;
    };
    f.after_write = [&](std::size_t) { CHECK(client.own_units().records[0].x == 9); };
    GameOptionChoices choices{};
    choices[1] = choices[2] = true;
    CHECK(configure_game_options(f.runtime, choices, f.game));
    CHECK(f.process.ordinary_preferences.game_options == 0x8000c028U);
    CHECK(client.own_units().records[0].x == 9);
}

TEST_CASE("F03 accepted options survive reported save failures while exceptions retain prefixes",
          "[configuration][session]") {
    SECTION("Open failure does not roll back options") {
        Fixture f;
        f.fail_open = true;
        CHECK(configure_game_options(f.runtime, GameOptionChoices{}, f.game));
        CHECK(f.runtime.world().globals.game_options == 0);
        CHECK(f.process.ordinary_preferences.game_options == 0);
        CHECK(f.trace == std::vector<std::string>{"stop", "open"});
    }
    SECTION("First failed write closes and deletes through the real preference writer") {
        Fixture f;
        f.fail_item = 0;
        CHECK(configure_game_options(f.runtime, GameOptionChoices{}, f.game));
        CHECK(f.trace == std::vector<std::string>{"stop", "open", "write:72", "close", "delete"});
        CHECK(f.process.ordinary_preferences.game_options == 0);
    }
    SECTION("A required audio stop retains its partial word without a save") {
        Fixture f;
        test::fail_service(f.game.reinitialize_music);
        GameOptionChoices choices{};
        choices.fill(true);
        CHECK_THROWS_AS(configure_game_options(f.runtime, choices, f.game), std::runtime_error);
        CHECK(f.runtime.world().globals.game_options == 0x18);
        CHECK(f.process.ordinary_preferences.game_options == 0xdeadbeefU);
        CHECK(f.trace.empty());
    }
}

TEST_CASE("F03 graphics checkboxes replace only the six graphics bits",
          "[configuration][session]") {
    constexpr std::array<std::uint32_t, 6> bits{0x20000, 0x40000,  0x200000,
                                                0x80000, 0x100000, 0x10000};
    for (std::size_t selected = 0; selected < bits.size(); ++selected) {
        Fixture f;
        f.runtime.world().globals.game_options = 0xffffffffU;
        test::fail_service(
            f.graphics.heralds_available); // Already enabled: no new availability query.
        GraphicsOptionChoices choices{};
        choices[selected] = true;
        CHECK(configure_graphics_options(f.runtime, choices, f.graphics));
        CHECK(f.runtime.world().globals.game_options == (0xffc0ffffU | bits[selected]));
        CHECK(f.process.ordinary_preferences.game_options ==
              f.runtime.world().globals.game_options);
        REQUIRE(f.written.size() == 2);
    }
}

TEST_CASE("F03 herald warning occurs in checkbox order and still enables heralds",
          "[configuration][session]") {
    Fixture f;
    GraphicsOptionChoices choices{};
    choices.fill(true);
    f.graphics.heralds_available = [&] {
        CHECK(f.runtime.world().globals.game_options == 0x60000);
        f.trace.push_back("available");
        return false;
    };
    f.graphics.low_memory_notice = [&] {
        CHECK(f.runtime.world().globals.game_options == 0x60000);
        CHECK(f.process.ordinary_preferences.game_options == 0xdeadbeefU);
        f.runtime.world().globals.game_options |= 0x80000020U;
        f.trace.push_back("warning");
    };
    CHECK(configure_graphics_options(f.runtime, choices, f.graphics));
    CHECK(f.runtime.world().globals.game_options == 0x803f0020U);
    CHECK(f.process.ordinary_preferences.game_options == 0x803f0020U);
    CHECK(f.trace == std::vector<std::string>{"available", "warning", "open", "write:72",
                                              "write:444", "close"});
}

TEST_CASE("F03 interrupted herald warning retains only preceding assignments",
          "[configuration][session]") {
    Fixture f;
    f.runtime.world().globals.game_options = 0x1f0010;
    f.graphics.heralds_available = [] { return false; };
    test::fail_service(f.graphics.low_memory_notice);
    GraphicsOptionChoices choices{};
    choices.fill(true);
    CHECK_THROWS_AS(configure_graphics_options(f.runtime, choices, f.graphics), std::runtime_error);
    CHECK(f.runtime.world().globals.game_options == 0x60010);
    CHECK(f.process.ordinary_preferences.game_options == 0xdeadbeefU);
    CHECK(f.written.empty());
}

TEST_CASE("F03 message replacement clears unknown bits and persists unchecked meanings",
          "[configuration][session]") {
    for (std::size_t unchecked = 0; unchecked <= 11; ++unchecked) {
        Fixture f;
        f.runtime.world().globals.message_options = 0xffff;
        MessageOptionChoices choices{};
        choices.fill(true);
        if (unchecked < choices.size())
            choices[unchecked] = false;
        CHECK(configure_message_options(f.runtime, choices, f.preferences));
        const auto expected = unchecked == 11 ? 0U : (1U << unchecked);
        CHECK(f.runtime.world().globals.message_options == expected);
        CHECK(f.process.ordinary_preferences.disabled_messages == expected);
        CHECK(f.process.ordinary_preferences.game_options == 0xdeadbeefU);
        REQUIRE(f.written.size() == 2);
        CHECK(formats::decode_ordinary_preferences(f.written[0]).disabled_messages == expected);
    }
    Fixture f;
    f.fail_open = true;
    CHECK(configure_message_options(f.runtime, MessageOptionChoices{}, f.preferences));
    CHECK(f.runtime.world().globals.message_options == 0x7ff);
    CHECK(f.process.ordinary_preferences.disabled_messages == 0x7ff);
}

TEST_CASE("F03 double movement preserves byte laws across all movement codes and domains",
          "[configuration][rules]") {
    auto rules = std::make_unique<state::RuleState>();
    std::size_t redraws = 0;
    const auto redraw = [&](std::int32_t argument) {
        CHECK(argument == 1);
        ++redraws;
    };
    for (unsigned code = 0; code < 256; ++code) {
        CAPTURE(code);
        rules->double_movement_applied = false;
        for (auto& unit : rules->definitions.units) {
            unit.domain = state::RuleDomain::land;
            unit.movement = static_cast<std::uint8_t>(code);
            unit.attack = 71;
        }
        rules->definitions.units[1].domain = state::RuleDomain::air;
        rules->definitions.units[2].domain = state::RuleDomain::sea;
        rules->definitions.units[3].domain = static_cast<state::RuleDomain>(255);
        rules::apply_double_movement(state::GameMode::hotseat, true, *rules, redraw);
        const auto doubled = (code * 2) % 256;
        CHECK(rules->double_movement_applied);
        CHECK(rules->definitions.units[0].movement == doubled);
        CHECK(rules->definitions.units[61].movement == doubled);
        CHECK(rules->definitions.units[1].movement == code);
        CHECK(rules->definitions.units[2].movement == code);
        CHECK(rules->definitions.units[3].movement == code);
        rules::apply_double_movement(state::GameMode::tcp, false, *rules, redraw);
        const auto signed_code =
            doubled < 128 ? static_cast<int>(doubled) : static_cast<int>(doubled) - 256;
        const auto halved = (signed_code / 2 + 256) % 256;
        CHECK_FALSE(rules->double_movement_applied);
        CHECK(rules->definitions.units[0].movement == halved);
        CHECK(rules->definitions.units[61].movement == halved);
        CHECK(rules->definitions.units[1].movement == code);
        CHECK(rules->definitions.units[61].attack == 71);
    }
    CHECK(redraws == 512);
}

TEST_CASE("F03 removal divides the signed current byte and never restores a remembered table",
          "[configuration][rules]") {
    auto rules = std::make_unique<state::RuleState>();
    rules->double_movement_applied = true;
    const std::array<std::uint8_t, 8> input{0, 1, 127, 128, 129, 253, 254, 255};
    const std::array<std::uint8_t, 8> expected{0, 0, 63, 192, 193, 255, 255, 0};
    for (std::size_t i = 0; i < input.size(); ++i)
        rules->definitions.units[i].movement = input[i];
    rules::apply_double_movement(state::GameMode::email, false, *rules, [](std::int32_t) {});
    for (std::size_t i = 0; i < expected.size(); ++i)
        CHECK(rules->definitions.units[i].movement == expected[i]);
}

TEST_CASE("F03 single-player and repeated doubling requests leave all state alone",
          "[configuration][rules]") {
    auto rules = std::make_unique<state::RuleState>();
    rules->definitions.units[0].movement = 200;
    rules->double_movement_applied = true;
    rules::apply_double_movement(state::GameMode::single_player, false, *rules, {});
    CHECK(rules->double_movement_applied);
    CHECK(rules->definitions.units[0].movement == 200);
    rules::apply_double_movement(state::GameMode::tcp, true, *rules, {});
    CHECK(rules->definitions.units[0].movement == 200);
    rules->double_movement_applied = false;
    rules::apply_double_movement(state::GameMode::single_player, true, *rules, {});
    rules::apply_double_movement(state::GameMode::tcp, false, *rules, {});
    CHECK_FALSE(rules->double_movement_applied);
    CHECK(rules->definitions.units[0].movement == 200);
}

TEST_CASE("F03 doubling publishes its flag and full table before redraw or a redraw stop",
          "[configuration][rules]") {
    auto rules = std::make_unique<state::RuleState>();
    rules->definitions.units[0].movement = 64;
    rules->definitions.units[61].movement = 127;
    CHECK_THROWS_AS(rules::apply_double_movement(state::GameMode::tcp, true, *rules, {}),
                    std::bad_function_call);
    CHECK(rules->double_movement_applied);
    CHECK(rules->definitions.units[0].movement == 128);
    CHECK(rules->definitions.units[61].movement == 254);
    rules::apply_double_movement(state::GameMode::tcp, false, *rules, [&](std::int32_t argument) {
        CHECK(argument == 1);
        CHECK_FALSE(rules->double_movement_applied);
        CHECK(rules->definitions.units[0].movement == 192);
        CHECK(rules->definitions.units[61].movement == 255);
        rules->double_movement_applied = true;
        rules->definitions.units[0].movement = 17;
    });
    CHECK(rules->double_movement_applied);
    CHECK(rules->definitions.units[0].movement == 17);
}

TEST_CASE("F03 missing configuration providers reject before effects", "[configuration][session]") {
    Fixture f;
    const auto globals = f.runtime.world().globals;
    const auto preferences = f.process.ordinary_preferences;
    GameOptionDependencies game{&f.preferences, nullptr};
    CHECK_THROWS_AS(configure_game_options(f.runtime, GameOptionChoices{}, game),
                    std::invalid_argument);
    GraphicsOptionDependencies graphics{nullptr, {}, {}};
    CHECK_THROWS_AS(configure_graphics_options(f.runtime, GraphicsOptionChoices{}, graphics),
                    std::invalid_argument);
    CHECK(f.runtime.world().globals == globals);
    CHECK(f.process.ordinary_preferences == preferences);
    CHECK(f.trace.empty());
}

TEST_CASE("F03 configuration invokes the same music provider across calls",
          "[configuration][session]") {
    Fixture f;
    std::vector<int> calls;
    f.game.stop_music = [count = 0, &calls]() mutable { calls.push_back(++count); };
    CHECK(configure_game_options(f.runtime, GameOptionChoices{}, f.game));
    CHECK(configure_game_options(f.runtime, GameOptionChoices{}, f.game));
    CHECK(calls == std::vector<int>{1, 2});
}

TEST_CASE(
    "F03 configuration pins world references through external callbacks before and after admission",
    "[configuration][session][lifetime]") {
    for (const bool active : {false, true}) {
        Fixture f;
        if (!active)
            f.runtime.end_client_session();
        const auto original = &f.runtime.world();
        const auto protected_callback = [&] {
            CHECK_THROWS_AS(f.runtime.prepare_new_game(), std::logic_error);
            CHECK_THROWS_AS(f.runtime.begin_client_session({1, 2}), std::logic_error);
            CHECK(&f.runtime.world() == original);
        };
        f.game.stop_music = protected_callback;
        f.game.reinitialize_music = protected_callback;
        f.game.synchronize_music = protected_callback;
        f.graphics.heralds_available = [&] {
            protected_callback();
            return false;
        };
        f.graphics.low_memory_notice = protected_callback;
        f.after_open = f.after_close = protected_callback;
        REQUIRE(configure_game_options(f.runtime, GameOptionChoices{false, false, true}, f.game));
        REQUIRE(configure_graphics_options(f.runtime, GraphicsOptionChoices{false, false, true},
                                           f.graphics));
        REQUIRE(configure_message_options(f.runtime, MessageOptionChoices{}, f.preferences));
        CHECK_NOTHROW(f.runtime.prepare_new_game());
    }
}
TEST_CASE("F03 configuration releases its world pin when a bound child throws",
          "[configuration][session][lifetime]") {
    Fixture f;
    f.game.stop_music = [] { throw std::runtime_error("authored failure"); };
    CHECK_THROWS_AS(configure_game_options(f.runtime, GameOptionChoices{}, f.game),
                    std::runtime_error);
    CHECK_NOTHROW(f.runtime.prepare_new_game());
}

// Composition copies only borrowing handles; implementations keep their identity.
static_assert(!std::is_copy_constructible_v<FileStorage>);
static_assert(!std::is_copy_constructible_v<FileChooser>);
static_assert(!std::is_copy_constructible_v<MusicControl>);
static_assert(!std::is_copy_constructible_v<CountdownControl>);
static_assert(!std::is_copy_constructible_v<OptionBroadcaster>);
static_assert(!std::is_copy_constructible_v<WorldSizeInteraction>);
static_assert(!std::is_copy_constructible_v<PlayerSetupInteraction>);
static_assert(!std::is_copy_constructible_v<ai::UnitMovementQueries>);
static_assert(!std::is_copy_constructible_v<unit::UnitDeletionEffects>);
static_assert(!std::is_copy_constructible_v<network::SocketTransport>);
static_assert(std::is_trivially_copyable_v<CoreLoadDependencies>);
static_assert(std::is_trivially_copyable_v<SaveMenuDependencies>);
static_assert(std::is_trivially_copyable_v<LoadDialogDependencies>);
static_assert(std::is_trivially_copyable_v<RuleContextDependencies>);
static_assert(std::is_trivially_copyable_v<GameOptionDependencies>);
static_assert(std::is_trivially_copyable_v<GraphicsOptionDependencies>);
static_assert(std::is_trivially_copyable_v<MultiplayerOptionDependencies>);
static_assert(std::is_trivially_copyable_v<UnitOperationDependencies>);
static_assert(std::is_trivially_copyable_v<WorldParameterDependencies>);
static_assert(std::is_trivially_copyable_v<PremadeWorldDependencies>);
static_assert(std::is_trivially_copyable_v<NewGameDependencies>);
static_assert(std::is_trivially_copyable_v<WorldEntryDependencies>);
static_assert(std::is_trivially_copyable_v<network::SendDependencies>);
