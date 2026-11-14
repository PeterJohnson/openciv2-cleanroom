// F03 section 8; N03 section 11 option proposals/shared vote callback; F01
// signed wrapping arithmetic. Supplied N02/P04 operations are explicit child
// outcomes, not claims of real transport or modal implementation.
#include "configuration_fixture.hpp"
#include <limits>

using namespace openciv;
using namespace openciv::session;
namespace {
struct Fixture : test::ConfigurationFixture {
    MultiplayerOptionServices services;
    std::function<void()> idle;
    std::vector<std::array<std::int32_t, 3>> sent;
    std::uint32_t ticks = 0;
    Fixture() {
        auto& multiplayer = runtime.world().multiplayer;
        runtime.world().globals.multiplayer = 3;
        multiplayer.network_host = true;
        multiplayer.player_count = 3;
        multiplayer.saved.simultaneous = -32768;
        process.multiplayer_preferences.double_production = 17;
        process.multiplayer_preferences.double_movement = -19;
        process.rules->definitions.units[0].movement = 64;
        process.rules->definitions.units[1].movement = 3;
        process.rules->definitions.units[1].domain = state::RuleDomain::air;
        process.rules->definitions.units[61].movement = 127;
        services.preferences = preferences;
        services.local_player_name = [&] {
            trace.push_back("name");
            return "Local player";
        };
        services.production_label = [&](bool enabled) {
            trace.push_back("production");
            return enabled ? "Production on" : "Production off";
        };
        services.movement_label = [&](bool enabled) {
            trace.push_back("movement");
            return enabled ? "Movement on" : "Movement off";
        };
        services.separator = [&] {
            trace.push_back("separator");
            return " / ";
        };
        services.broadcast_proposal = [&] { trace.push_back("proposal"); };
        services.broadcast_rejection = [&] { trace.push_back("rejected"); };
        services.broadcast_acceptance = [&] { trace.push_back("accepted"); };
        services.install_vote_callback = [&](std::function<void()> callback) {
            trace.push_back("install");
            idle = std::move(callback);
        };
        services.show_vote_dialog = [&] {
            trace.push_back("modal");
            multiplayer.vote.pending = 0;
            return 0;
        };
        services.vote.clock = [&] {
            trace.push_back("clock");
            return ++ticks;
        };
        services.vote.poll = [&](std::int32_t first, std::int32_t second) {
            CHECK(first == 1);
            CHECK(second == 0);
            trace.push_back("poll");
        };
        services.vote.finish_modal = [&] { trace.push_back("finish"); };
        services.redraw_status = [&](std::int32_t argument) {
            CHECK(argument == 1);
            trace.push_back("redraw");
        };
        services.send_units = [&] { trace.push_back("units"); };
        services.send_options = [&](std::int32_t production, std::int32_t movement,
                                    std::int32_t simultaneous) {
            trace.push_back("options");
            sent.push_back({production, movement, simultaneous});
        };
        services.notice = [&](std::string_view name) { trace.emplace_back(name); };
    }
    MultiplayerOptionResult apply(MultiplayerOptionChoices choices = {true, false, true, true},
                                  std::int32_t result = 0) {
        return configure_multiplayer_options(runtime, result, choices, services);
    }
};
} // namespace

TEST_CASE("N03 multiplayer options only accept dialog result exactly zero",
          "[configuration][multiplayer]") {
    for (const auto result : {-1, 1, 256}) {
        Fixture f;
        const auto preferences = f.process.multiplayer_preferences;
        const auto multiplayer = f.runtime.world().multiplayer;
        CHECK(f.apply({}, result) == MultiplayerOptionResult::cancelled);
        CHECK(f.process.multiplayer_preferences == preferences);
        CHECK(f.runtime.world().multiplayer == multiplayer);
        CHECK(f.trace.empty());
    }
}

TEST_CASE("N03 accepted host changes save chat first then vote transform and publish",
          "[configuration][multiplayer]") {
    Fixture f;
    auto& world = f.runtime.world();
    auto& rules = *f.process.rules;
    world.entities.units.storage()[0].moves_used = 29;
    world.entities.units.set_end(1);
    const auto random = f.runtime.gameplay_random().checkpoint();
    f.services.broadcast_proposal = [&] {
        CHECK(world.multiplayer.vote.pending == 2);
        CHECK(world.multiplayer.vote.status == 0);
        CHECK(f.process.text_parameters.strings[0] == "Local player");
        CHECK(f.process.text_parameters.strings[1] == "Production on / Movement on");
        CHECK(world.multiplayer.saved.double_production == 0);
        CHECK(world.multiplayer.saved.double_movement == 0);
        f.trace.push_back("proposal");
    };
    f.services.redraw_status = [&](std::int32_t arg) {
        CHECK(arg == 1);
        CHECK(rules.double_movement_applied);
        CHECK(rules.definitions.units[0].movement == 128);
        CHECK(rules.definitions.units[61].movement == 254);
        CHECK(rules.definitions.units[1].movement == 3);
        CHECK(world.multiplayer.saved.double_production == 1);
        CHECK(world.multiplayer.saved.double_movement == 1);
        f.trace.push_back("redraw");
    };
    CHECK(f.apply() == MultiplayerOptionResult::applied);
    CHECK(f.trace == std::vector<std::string>{"open", "write:72", "write:444", "close", "name",
                                              "production", "separator", "movement", "proposal",
                                              "install", "clock", "modal", "redraw", "units",
                                              "options", "accepted", "PMCHANGEYES"});
    CHECK(f.sent == std::vector<std::array<std::int32_t, 3>>{{1, 1, -32768}});
    REQUIRE(f.written.size() == 2);
    const auto saved = formats::decode_multiplayer_preferences(f.written[1]);
    CHECK(saved.clear_chat_new_game == 1);
    CHECK(saved.clear_chat_boot == 0);
    CHECK(saved.double_production == 17);
    CHECK(saved.double_movement == -19);
    CHECK(f.process.multiplayer_preferences.double_production == 1);
    CHECK(f.process.multiplayer_preferences.double_movement == 1);
    CHECK(world.entities.units.storage()[0].moves_used == 29);
    CHECK(f.runtime.gameplay_random().checkpoint() == random);
}

TEST_CASE("N03 network clients stop after chat persistence and host authority is read afterward",
          "[configuration][multiplayer]") {
    for (const bool host_after_save : {false, true}) {
        Fixture f;
        f.runtime.world().multiplayer.network_host = !host_after_save;
        f.after_close = [&] { f.runtime.world().multiplayer.network_host = host_after_save; };
        CHECK(f.apply() == (host_after_save ? MultiplayerOptionResult::applied
                                            : MultiplayerOptionResult::client_only));
        if (!host_after_save) {
            CHECK(f.trace == std::vector<std::string>{"open", "write:72", "write:444", "close"});
            CHECK(f.process.multiplayer_preferences.double_production == 17);
            CHECK(f.process.multiplayer_preferences.double_movement == -19);
            CHECK(f.runtime.world().multiplayer.saved.double_movement == 0);
            CHECK_FALSE(f.idle);
        }
        REQUIRE(f.written.size() == 2);
        CHECK(formats::decode_multiplayer_preferences(f.written[1]).clear_chat_new_game == 1);
    }
}

TEST_CASE("N03 unchanged proposals normalize preferences without reapplying movement",
          "[configuration][multiplayer]") {
    Fixture f;
    f.runtime.world().multiplayer.saved.double_production = 1;
    f.runtime.world().multiplayer.saved.double_movement = 1;
    CHECK(f.apply() == MultiplayerOptionResult::unchanged);
    CHECK(f.process.multiplayer_preferences.double_production == 1);
    CHECK(f.process.multiplayer_preferences.double_movement == 1);
    CHECK_FALSE(f.process.rules->double_movement_applied);
    CHECK(f.process.rules->definitions.units[0].movement == 64);
    CHECK(f.trace == std::vector<std::string>{"open", "write:72", "write:444", "close"});
    CHECK(formats::decode_multiplayer_preferences(f.written[1]).double_movement == -19);
}

TEST_CASE("N03 local modes and a one-player host apply without a vote or network publication",
          "[configuration][multiplayer]") {
    for (const auto mode : std::array<std::uint8_t, 4>{0, 1, 2, 3}) {
        Fixture f;
        auto& multiplayer = f.runtime.world().multiplayer;
        f.runtime.world().globals.multiplayer = mode;
        multiplayer.player_count = 1;
        multiplayer.network_host = mode == 3;
        f.services.local_player_name = {};
        CHECK(f.apply() == MultiplayerOptionResult::applied);
        CHECK(multiplayer.saved.double_production == 1);
        CHECK(multiplayer.saved.double_movement == 1);
        CHECK(f.process.rules->double_movement_applied == (mode != 0));
        CHECK(f.process.rules->definitions.units[0].movement == (mode == 0 ? 64 : 128));
        CHECK(f.sent.empty());
        CHECK_FALSE(f.idle);
    }
}

TEST_CASE("N03 reported chat save failure still reaches the accepted double proposal",
          "[configuration][multiplayer]") {
    Fixture f;
    f.fail_item = 0;
    CHECK(f.apply() == MultiplayerOptionResult::applied);
    REQUIRE(f.trace.size() > 4);
    CHECK(std::vector<std::string>(f.trace.begin(), f.trace.begin() + 4) ==
          std::vector<std::string>{"open", "write:72", "close", "delete"});
    CHECK(f.written.size() == 1);
    CHECK(f.runtime.world().multiplayer.saved.double_movement == 1);
    CHECK(f.process.multiplayer_preferences.clear_chat_new_game == 1);
}

TEST_CASE("N03 modal rejection status and every nonzero pending count restore live double values",
          "[configuration][multiplayer]") {
    struct Reply {
        std::int32_t modal, status, pending;
        bool accepted;
    };
    for (const auto reply : {Reply{-1, 0, 0, false}, Reply{0, -1, 0, false}, Reply{0, 0, 1, false},
                             Reply{0, 0, -1, false}, Reply{5, 9, 0, true}, Reply{0, -2, 0, true}}) {
        Fixture f;
        auto& multiplayer = f.runtime.world().multiplayer;
        f.services.show_vote_dialog = [&] {
            multiplayer.saved.double_production = -7;
            multiplayer.saved.double_movement = 2;
            multiplayer.vote.status = reply.status;
            multiplayer.vote.pending = reply.pending;
            return reply.modal;
        };
        CHECK(f.apply() == (reply.accepted ? MultiplayerOptionResult::applied
                                           : MultiplayerOptionResult::rejected));
        if (!reply.accepted) {
            CHECK(f.process.multiplayer_preferences.double_production == -7);
            CHECK(f.process.multiplayer_preferences.double_movement == 2);
            CHECK(multiplayer.saved.double_production == -7);
            CHECK(multiplayer.saved.double_movement == 2);
            CHECK_FALSE(f.process.rules->double_movement_applied);
            CHECK(f.process.rules->definitions.units[0].movement == 64);
            CHECK(f.trace[f.trace.size() - 2] == "rejected");
            CHECK(f.trace.back() == "PMCHANGENO");
            CHECK(f.sent.empty());
        }
        CHECK(f.written.size() == 2); // No second save after rejection or acceptance.
    }
}

TEST_CASE("N03 proposal text uses live preference and player-count reads after child callbacks",
          "[configuration][multiplayer]") {
    Fixture f;
    auto& multiplayer = f.runtime.world().multiplayer;
    f.services.local_player_name = [] { return std::string("Local\0ignored", 13); };
    f.services.production_label = [&](bool enabled) {
        CHECK(enabled);
        f.process.multiplayer_preferences.double_movement = 0;
        return "Production on";
    };
    f.services.separator = [&] {
        multiplayer.player_count = std::numeric_limits<std::int32_t>::min();
        return " / ";
    };
    f.services.broadcast_proposal = [&] {
        CHECK(f.process.text_parameters.strings[0] == "Local");
        CHECK(f.process.text_parameters.strings[1] == "Production on / Movement off");
        CHECK(multiplayer.vote.pending == std::numeric_limits<std::int32_t>::max());
    };
    CHECK(f.apply() == MultiplayerOptionResult::applied);
    CHECK(multiplayer.saved.double_production == 1);
    CHECK(multiplayer.saved.double_movement == 0);
    CHECK_FALSE(f.process.rules->double_movement_applied);
    CHECK(f.sent.empty());
    CHECK(f.written.size() == 2);
}

TEST_CASE("N03 post-transformation gates are live and a selected publication tail finishes",
          "[configuration][multiplayer]") {
    Fixture f;
    auto& world = f.runtime.world();
    world.globals.multiplayer = 1;
    world.multiplayer.player_count = 1;
    f.services.redraw_status = [&](std::int32_t) {
        world.globals.multiplayer = 3;
        world.multiplayer.player_count = 2;
        f.trace.push_back("redraw");
    };
    f.services.send_units = [&] {
        world.globals.multiplayer = 0;
        world.multiplayer.player_count = 0;
        world.multiplayer.saved.double_production = -3;
        world.multiplayer.saved.double_movement = -4;
        world.multiplayer.saved.simultaneous = 0x1234;
        f.trace.push_back("units");
    };
    CHECK(f.apply() == MultiplayerOptionResult::applied);
    CHECK(f.sent == std::vector<std::array<std::int32_t, 3>>{{-3, -4, 0x1234}});
    CHECK(f.trace == std::vector<std::string>{"open", "write:72", "write:444", "close", "redraw",
                                              "units", "options", "accepted", "PMCHANGEYES"});
    CHECK(f.process.rules->definitions.units[0].movement == 128);
}

TEST_CASE("N03 redraw can suppress later network publication after an accepted vote",
          "[configuration][multiplayer]") {
    Fixture f;
    f.services.redraw_status = [&](std::int32_t) {
        f.runtime.world().multiplayer.player_count = 1;
    };
    f.services.send_units = {};
    CHECK(f.apply() == MultiplayerOptionResult::applied);
    CHECK(f.runtime.world().multiplayer.saved.double_movement == 1);
    CHECK(f.sent.empty());
    CHECK(f.trace.back() == "modal");
}

TEST_CASE("N03 vote and publication exceptions retain completed options without invented replies",
          "[configuration][multiplayer]") {
    SECTION("Missing modal retains pending proposal after the first preference save") {
        Fixture f;
        f.services.show_vote_dialog = {};
        CHECK_THROWS_AS(f.apply(), std::bad_function_call);
        CHECK(f.process.multiplayer_preferences.double_movement == 1);
        CHECK(f.runtime.world().multiplayer.saved.double_movement == 0);
        CHECK(f.runtime.world().multiplayer.vote.pending == 2);
        CHECK(f.runtime.world().multiplayer.vote.popup_tick == 1);
        CHECK(f.written.size() == 2);
        CHECK(static_cast<bool>(f.idle));
    }
    SECTION("Missing send retains accepted options and transformed rules") {
        Fixture f;
        f.services.send_units = {};
        CHECK_THROWS_AS(f.apply(), std::bad_function_call);
        CHECK(f.runtime.world().multiplayer.saved.double_movement == 1);
        CHECK(f.process.rules->double_movement_applied);
        CHECK(f.process.rules->definitions.units[0].movement == 128);
        CHECK(f.trace.back() == "redraw");
    }
}

TEST_CASE("N03 shared vote callback uses its signed wrapping 2401-tick deadline",
          "[configuration][multiplayer][vote]") {
    struct Case {
        std::uint32_t start, now;
        bool waits;
    };
    for (const auto value : {Case{10, 2410, true}, Case{10, 2411, false},
                             Case{0xfffffff0U, 0x10U, true}, Case{0, 0x80000000U, true}}) {
        Fixture f;
        auto& vote = f.runtime.world().multiplayer.vote;
        vote = {-2, -2, value.start};
        std::size_t clocks = 0;
        f.services.vote.clock = [&] {
            ++clocks;
            return clocks == 1 ? value.now : 77U;
        };
        poll_option_vote(f.runtime, f.services.vote);
        CHECK(clocks == (value.waits ? 1 : 2));
        CHECK(vote.popup_tick == (value.waits ? value.start : 77U));
        CHECK(f.trace == (value.waits ? std::vector<std::string>{"poll"}
                                      : std::vector<std::string>{"poll", "finish"}));
        CHECK(vote.pending == -2);
        CHECK(vote.status == -2);
    }
}

TEST_CASE("N03 vote callback polls first and short-circuits the first clock only for handoff",
          "[configuration][multiplayer][vote]") {
    for (const auto reason : {0, 1, 2}) {
        Fixture f;
        auto& multiplayer = f.runtime.world().multiplayer;
        multiplayer.vote = {2, 0, 100};
        f.services.vote.poll = [&](std::int32_t a, std::int32_t b) {
            CHECK(a == 1);
            CHECK(b == 0);
            f.trace.push_back("poll");
            if (reason == 0)
                multiplayer.handoff = -1;
            else if (reason == 1)
                multiplayer.vote.pending = 0;
            else
                multiplayer.vote.status = -1;
        };
        poll_option_vote(f.runtime, f.services.vote);
        CHECK(f.trace == (reason == 0
                              ? std::vector<std::string>{"poll", "finish", "clock"}
                              : std::vector<std::string>{"poll", "clock", "finish", "clock"}));
        CHECK(multiplayer.vote.popup_tick == (reason == 0 ? 1U : 2U));
        CHECK_FALSE(f.runtime.world().turn_loop_active); // No loop-active gate was added.
    }
}

TEST_CASE("N03 vote callback observes clock mutations and stores the fresh post-completion tick",
          "[configuration][multiplayer][vote]") {
    Fixture f;
    auto& vote = f.runtime.world().multiplayer.vote;
    vote = {1, 0, 0};
    f.services.vote.clock = [&] {
        vote.popup_tick = 2999;
        return 3000U;
    };
    poll_option_vote(f.runtime, f.services.vote);
    CHECK(f.trace == std::vector<std::string>{"poll"});
    CHECK(vote.popup_tick == 2999);
    f.trace.clear();
    std::size_t clocks = 0;
    f.services.vote.clock = [&] {
        f.trace.push_back("clock");
        if (++clocks == 1) {
            vote.status = -1;
            return 3000U;
        }
        return 5000U;
    };
    f.services.vote.finish_modal = [&] {
        vote.popup_tick = 9000;
        f.trace.push_back("finish");
    };
    poll_option_vote(f.runtime, f.services.vote);
    CHECK(vote.popup_tick == 5000);
    CHECK(f.trace == std::vector<std::string>{"poll", "clock", "finish", "clock"});
}

TEST_CASE("N03 installed vote callback runs the real timeout path inside the supplied modal",
          "[configuration][multiplayer][vote]") {
    Fixture f;
    auto& vote = f.runtime.world().multiplayer.vote;
    f.services.vote.clock = [&] { return ++f.ticks == 1 ? 100U : f.ticks == 2 ? 2501U : 2600U; };
    bool completed = false;
    f.services.vote.finish_modal = [&] { completed = true; };
    f.services.show_vote_dialog = [&] {
        REQUIRE(static_cast<bool>(f.idle));
        CHECK(vote.popup_tick == 100);
        f.idle();
        CHECK(completed);
        CHECK(vote.popup_tick == 2600);
        CHECK(vote.pending == 2);
        return 0;
    };
    CHECK(f.apply() == MultiplayerOptionResult::rejected);
    CHECK(f.process.multiplayer_preferences.double_production == 0);
    CHECK(f.process.multiplayer_preferences.double_movement == 0);
    CHECK_FALSE(f.process.rules->double_movement_applied);
    CHECK(f.trace.back() == "PMCHANGENO");
}
